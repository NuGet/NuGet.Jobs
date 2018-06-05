// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery;

namespace Validation.PackageSigning.RepositorySign
{
    using IGalleryContext = IEntitiesContext;

    public class Initializer
    {
        private static int BatchSize = 1000;

        private static readonly string[] PreinstalledPaths = new[]
        {
            "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackages",
            // TODO: "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackagesFallback",
            // TODO: "C:\\Program Files (x86)\\Microsoft SDKs\\UWPNuGetPackages",
            "%USERPROFILE%\\.nuget\\packages", // TODO: I don't think these are the fallback folders.
            "%USERPROFILE%\\.nuget\\packages\\.tools"
        };

        private readonly IGalleryContext _galleryContext;
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ILogger<Initializer> _logger;

        public Initializer(
            IGalleryContext galleryContext,
            IValidationEntitiesContext validationContext,
            ILogger<Initializer> logger)
        {
            // TODO: Accept configuration! (InitializationMaxPackageKey, InitializationSleepDurationBetweenBatchesInSeconds)
            // TODO: Accept service for settings (IsInitialized, etc...)
            _galleryContext = galleryContext ?? throw new ArgumentNullException(nameof(galleryContext));
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            await ClearPackageRevalidationStateAsync();

            // Find packages owned by Microsoft or preinstalled by Visual Studio.
            var microsoftPackages = FindMicrosoftPackages();
            var preinstalledPackages = FindPreinstalledPackages(except: microsoftPackages);

            // Find the packages depended by both Microsoft and preinstalled packages.
            var microsoftOrPreinstalledPackages = new CaseInsensitiveSet();

            microsoftOrPreinstalledPackages.UnionWith(microsoftPackages);
            microsoftOrPreinstalledPackages.UnionWith(preinstalledPackages);

            var dependencyPackages = FindDependencyPackages(microsoftOrPreinstalledPackages);

            // Find all other package ids.
            var knownPackages = new CaseInsensitiveSet();

            knownPackages.UnionWith(microsoftOrPreinstalledPackages);
            knownPackages.UnionWith(dependencyPackages);

            // TODO: Need an upper limit on PackageKey that prevents packages that have already been reposigned.
            var remainingPackages = FindRemainingPackages(except: knownPackages);

            // Save the packages that were found, by order of priority.
            InitializePackageSet("Microsoft", microsoftPackages);
            InitializePackageSet("Preinstalled", preinstalledPackages);
            InitializePackageSet("Dependency", dependencyPackages);
            InitializePackageSet("Remaining", remainingPackages);

            // TODO: Set "IsInitialized" setting to true
        }

        private async Task ClearPackageRevalidationStateAsync()
        {
            _logger.LogInformation("Clearing package revalidation state, if it exists...");

            // TODO
            await Task.Yield();

            _logger.LogInformation("Cleared package revalidation state");
        }

        private CaseInsensitiveSet FindMicrosoftPackages()
        {
            var packages = _galleryContext.PackageRegistrations
                .Where(r => r.Owners.Any(o => o.Username == "Microsoft"))
                .Select(r => r.Id);

            return new CaseInsensitiveSet(packages);
        }

        private CaseInsensitiveSet FindPreinstalledPackages(HashSet<string> except)
        {
            var preinstalledPackages = new CaseInsensitiveSet();

            foreach (var path in PreinstalledPaths)
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(path);
                var packagesInPath = Directory.GetDirectories(expandedPath)
                    .Select(d => d.Replace(expandedPath, "").Trim('\\').ToLowerInvariant())
                    .Where(d => !d.StartsWith("."));

                preinstalledPackages.UnionWith(packagesInPath);
            }

            preinstalledPackages.ExceptWith(except);

            return preinstalledPackages;
        }

        private CaseInsensitiveSet FindDependencyPackages(HashSet<string> roots)
        {
            var result = new CaseInsensitiveSet();
            var next = new CaseInsensitiveSet(roots);
            var all = new CaseInsensitiveSet(roots);

            do
            {
                var dependencies = _galleryContext.Set<PackageDependency>()
                    .Where(d => !all.Contains(d.Id))
                    .Where(d => next.Contains(d.Package.PackageRegistration.Id))
                    .Select(d => d.Id)
                    .Distinct()
                    .ToList();

                next.Clear();

                result.UnionWith(dependencies);
                next.UnionWith(dependencies);
                all.UnionWith(dependencies);
            }
            while (next.Count() > 0);

            return result;
        }

        private CaseInsensitiveSet FindRemainingPackages(HashSet<string> except)
        {
            // TODO: Test this on PROD as this may not scale.
            var packages = _galleryContext.PackageRegistrations
                .Where(r => !except.Contains(r.Id))
                .Select(r => r.Id)
                .Distinct();

            return new CaseInsensitiveSet(packages);
        }

        private void InitializePackageSet(string setName, CaseInsensitiveSet packageIds)
        {
            var batches = PartitionIntoBatchesOrderedByDownloads(setName, packageIds);

            // Initialize each package id. Each version of the package will be persisted, in descending order.
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                _logger.LogInformation("Initializing batch {BatchIndex} of package set '{SetName}'...",
                    batchIndex,
                    setName);

                var batch = batches[batchIndex];
                var versions = _galleryContext.Set<Package>()
                    .Where(p => batch.Contains(p.PackageRegistration.Id))
                    .GroupBy(p => p.PackageRegistration.Id)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(p => p.NormalizedVersion)
                            .Select(v => NuGetVersion.Parse(v))
                            .OrderByDescending(v => v),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var packageId in batch)
                {
                    foreach (var version in versions[packageId])
                    {
                        // TODO: Insert record
                        Console.WriteLine($"Revalidate {packageId} {version}");
                    }
                }

                // TODO: Persist records.

                _logger.LogInformation("Initialized batch {BatchIndex} of package set '{SetName}'",
                    batchIndex,
                    setName);

                // TODO: Sleep if not last
            }

            _logger.LogInformation("Finished initializing package set '{SetName}'", setName);
        }

        private List<List<string>> PartitionIntoBatchesOrderedByDownloads(string setName, CaseInsensitiveSet packageIds)
        {
            _logger.LogInformation(
                "Partitioning package set '{SetName}' into batches...",
                setName);

            var packages = FindPackageInformation(packageIds);
            var orderedPackages = packages.OrderByDescending(p => p.Downloads).ToList();

            var batches = new List<List<string>>();
            var current = new List<string>();
            var currentSize = 0;

            foreach (var package in orderedPackages)
            {
                if (currentSize + package.Versions > BatchSize)
                {
                    batches.Add(current);
                    current = new List<string>();
                    currentSize = 0;
                }

                current.Add(package.Id);
                currentSize += package.Versions;
            }

            if (current.Count > 0)
            {
                batches.Add(current);
            }

            _logger.LogInformation(
                "Partitioned package set '{SetName}' into {BatchCount} batches",
                setName,
                batches.Count);

            return batches;
        }

        private List<PackageInformation> FindPackageInformation(CaseInsensitiveSet packageIds)
        {
            // Batch the package ids into chunks that are manageable.
            var packageIdList = packageIds.ToList();
            var batches = new List<CaseInsensitiveSet>();
            var current = new CaseInsensitiveSet();

            foreach (var packageId in packageIds)
            {
                if (current.Count + 1 > BatchSize)
                {
                    batches.Add(current);
                    current = new CaseInsensitiveSet();
                }

                current.Add(packageId);
            }

            if (current.Count > 0)
            {
                batches.Add(current);
            }

            // For each package batch, fetch that package's information.
            var result = new List<PackageInformation>();

            foreach (var batch in batches)
            {
                var packages = _galleryContext.Set<Package>()
                    .Where(p => batch.Contains(p.PackageRegistration.Id))
                    .GroupBy(p => p.PackageRegistration.Id)
                    .Select(g => new PackageInformation
                    {
                        Id = g.Key,
                        Downloads = g.Sum(p => p.DownloadCount),
                        Versions = g.Count(),
                    });

                result.AddRange(packages);
            }

            return result;
        }

        private class CaseInsensitiveSet : HashSet<string>
        {
            public CaseInsensitiveSet()
                : base(StringComparer.OrdinalIgnoreCase)
            {
            }

            public CaseInsensitiveSet(IEnumerable<string> collection)
                : base(collection, StringComparer.OrdinalIgnoreCase)
            {
            }
        }

        private class PackageInformation
        {
            public string Id { get; set; }
            public int Downloads { get; set; }
            public int Versions { get; set; }
        }
    }
}
