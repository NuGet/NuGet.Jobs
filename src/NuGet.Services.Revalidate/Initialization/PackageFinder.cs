// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using IGalleryContext = IEntitiesContext;

    public class PackageFinder : IPackageFinder
    {
        private static int BatchSize = 1000;
        private static string MicrosoftAccountName = "Microsoft";

        private readonly IGalleryContext _galleryContext;
        private readonly InitializationConfiguration _config;
        private readonly ILogger<PackageFinder> _logger;

        public PackageFinder(
            IGalleryContext galleryContext,
            InitializationConfiguration config,
            ILogger<PackageFinder> logger)
        {
            _galleryContext = galleryContext ?? throw new ArgumentNullException(nameof(galleryContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaseInsensitiveSet FindMicrosoftPackages()
        {
            var packages = _galleryContext.PackageRegistrations
                .Where(r => r.Owners.Any(o => o.Username == MicrosoftAccountName))
                .Select(r => r.Id);

            return new CaseInsensitiveSet(packages);
        }

        public CaseInsensitiveSet FindPreinstalledPackages(CaseInsensitiveSet except)
        {
            var preinstalledPackages = new CaseInsensitiveSet();

            foreach (var path in _config.PreinstalledPaths)
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

        public CaseInsensitiveSet FindDependencyPackages(CaseInsensitiveSet roots)
        {
            var result = new CaseInsensitiveSet();
            var next = new CaseInsensitiveSet(roots);
            var all = new CaseInsensitiveSet(roots);

            do
            {
                // TODO: Test this on PROD as this may not scale.
                var dependencies = _galleryContext.Set<PackageDependency>()
                    .Where(d => !all.Contains(d.Id))
                    .Where(d => next.Contains(d.Package.PackageRegistration.Id))
                    .Select(d => d.Id)
                    .ToList();

                next.Clear();

                result.UnionWith(dependencies);
                next.UnionWith(dependencies);
                all.UnionWith(dependencies);
            }
            while (next.Count() > 0);

            return result;
        }

        public CaseInsensitiveSet FindAllPackages(CaseInsensitiveSet except)
        {
            // TODO: Test this on PROD as this may not scale.
            var packages = _galleryContext.PackageRegistrations
                .Where(r => !except.Contains(r.Id))
                .Select(r => r.Id);

            return new CaseInsensitiveSet(packages);
        }

        public List<PackageInformation> FindPackageInformation(string setName, CaseInsensitiveSet packageIds)
        {
            // Fetch the packages' information in batches.
            var batches = packageIds.Batch(BatchSize);
            var result = new List<PackageInformation>();

            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                _logger.LogInformation(
                    "Fetching batch {Batch} of {BatchesCount} of package informations for package set {SetName}...",
                    batchIndex + 1,
                    batches.Count,
                    setName);

                var batch = batches[batchIndex];

                var packages = _galleryContext.Set<Package>()
                    .Where(p => packageIds.Contains(p.PackageRegistration.Id))
                    .GroupBy(p => p.PackageRegistration.Id)
                    .Select(g => new PackageInformation
                    {
                        Id = g.Key,
                        Downloads = g.Sum(p => p.DownloadCount),
                        Versions = g.Count(),
                    });

                result.AddRange(packages);

                _logger.LogInformation(
                    "Fetched batch {Batch} of {BatchesCount} of package informations for package set {SetName}",
                    batchIndex + 1,
                    batches.Count,
                    setName);
            }

            return result;
        }

        public Dictionary<string, List<NuGetVersion>> FindAppropriateVersions(List<string> packageIds)
        {
            var maxCreated = _config.MaxPackageCreationDate.UtcDateTime;

            return _galleryContext.Set<Package>()
                .Where(p => packageIds.Contains(p.PackageRegistration.Id))
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Deleted)
                .Where(p => p.Created < maxCreated)
                .GroupBy(p => p.PackageRegistration.Id)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.NormalizedVersion)
                        .Select(NuGetVersion.Parse)
                        .OrderByDescending(v => v)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
