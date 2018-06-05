// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Versioning;
using NuGetGallery;

namespace Validation.PackageSigning.RepositorySign
{
    using GalleryContext = EntitiesContext;
    using IGalleryContext = IEntitiesContext;

    public class Job : JsonConfigurationJob
    {
        private const string InitializeArgumentName = "Initialize";
        private const string JobConfigurationSectionName = "RepositorySignJob";

        private bool _initialize;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _initialize = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, InitializeArgumentName);

            if (_initialize && !JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once))
            {
                throw new Exception($"Argument {JobArgumentNames.Once} is required if argument {InitializeArgumentName} is present.");
            }
        }

        public override async Task Run()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                if (_initialize)
                {
                    Logger.LogInformation("Initializing Repository Sign job...");

                    await scope.ServiceProvider
                        .GetRequiredService<Initializer>()
                        .InitializeAsync();

                    Logger.LogInformation("Repository Sign job initialized");
                }
                else
                {
                    // TODO
                    throw new NotImplementedException();
                }
            }
        }

        public async Task RunOld()
        {
            var paths = new[]
            {
                "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackages",
                // TODO: "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackagesFallback",
                // TODO: "C:\\Program Files (x86)\\Microsoft SDKs\\UWPNuGetPackages",
                "%USERPROFILE%\\.nuget\\packages",
                "%USERPROFILE%\\.nuget\\packages\\.tools"
            };

            using (var scope = _serviceProvider.CreateScope())
            {
                var microsoftPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var context = scope.ServiceProvider.GetRequiredService<IGalleryContext>();

                // Get packages owned by Microsoft
                var packages = context.PackageRegistrations
                    .Where(r => r.Owners.Any(o => o.Username == "Microsoft"))
                    .Select(r => r.Id);

                microsoftPackages.UnionWith(packages);

                // Get packages in preinstalled paths that aren't owned by Microsoft.
                var preinstalledPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    var expandedPath = Environment.ExpandEnvironmentVariables(path);
                    var packagesInPath = Directory.GetDirectories(expandedPath)
                        .Select(d => d.Replace(expandedPath, "").Trim('\\').ToLowerInvariant())
                        .Where(d => !d.StartsWith("."));

                    preinstalledPackages.UnionWith(packagesInPath);
                }

                preinstalledPackages.ExceptWith(microsoftPackages);

                // Get set of dependencies of Microsoft packages and pre-installed packages.
                var microsoftOrPreinstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                microsoftOrPreinstalled.UnionWith(microsoftPackages);
                microsoftOrPreinstalled.UnionWith(preinstalledPackages);

                var dependencies = context.Set<PackageDependency>()
                    .Where(d => !microsoftOrPreinstalled.Contains(d.Id))
                    .Where(d => microsoftOrPreinstalled.Contains(d.Package.PackageRegistration.Id))
                    .Select(d => d.Id);

                // TODO: This only gets first level of dependencies. This should iterate and get all dependencies!
                var dependencyPackages = new HashSet<string>(dependencies, StringComparer.OrdinalIgnoreCase);

                // TODO: Insert microsoftPackages, preinstalledPackages, and dependencyPackages into table.
                // For each hash set, order package ids by total downloads. For each package id, get all versions
                // and insert them into database by descending order of versions.
                await InsertPackageIdSetAsync(context, microsoftPackages);
                await InsertPackageIdSetAsync(context, preinstalledPackages);
                await InsertPackageIdSetAsync(context, dependencyPackages);
            }

            async Task InsertPackageIdSetAsync(IGalleryContext context, HashSet<string> packageIds)
            {
                // Partition the set into batches of ids, ordered by their downloads.
                // TODO: Log "Partioning package set into batches..."
                var packages = context.Set<Package>()
                    .Where(p => packageIds.Contains(p.PackageRegistration.Id))
                    .GroupBy(p => p.PackageRegistration.Id)
                    .Select(g => new
                    {
                        Id = g.Key,
                        Count = g.Count(),
                        Downloads = g.Sum(p => p.DownloadCount)
                    })
                    .OrderByDescending(p => p.Downloads)
                    .ToList();

                int batchSize = 1000;

                var batches = new List<List<string>>();
                var current = new List<string>();

                foreach (var package in packages)
                {
                    if (current.Count + package.Count > batchSize)
                    {
                        batches.Add(current);
                        current = new List<string>();
                    }

                    current.Add(package.Id);
                }

                if (current.Count > 0)
                {
                    batches.Add(current);
                }

                // TODO: Log "Partitioned package ids into X batches"

                // For each batch of package ids, find the list of versions that should be persisted in the database.
                foreach (var batch in batches)
                {
                    var versions = context.Set<Package>()
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
                            Console.WriteLine($"Revalidate {packageId} {version}");
                        }
                    }
                }
            }

            await Task.Yield();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(JobConfigurationSectionName));

            services.AddScoped<IGalleryContext>(provider =>
            {
                var config = provider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value;

                return new GalleryContext(config.ConnectionString, readOnly: false);
            });

            services.AddScoped<Initializer>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}