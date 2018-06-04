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
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGetGallery;

namespace Validation.PackageSigning.RepositorySign
{
    using GalleryContext = EntitiesContext;
    using IGalleryContext = IEntitiesContext;

    public class Job : JsonConfigurationJob
    {
        private const string JobConfigurationSectionName = "RepositorySignJob";

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            // TODO: Get required services here
        }

        public override async Task Run()
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
                var microsoftPackages = new HashSet<string>();

                var context = scope.ServiceProvider.GetRequiredService<IGalleryContext>();

                // Get packages owned by Microsoft
                var packages = context.PackageRegistrations
                    .Where(r => r.Owners.Any(o => o.Username == "Microsoft"))
                    .Select(r => r.Id.ToLowerInvariant())
                    .ToList();

                microsoftPackages.UnionWith(packages);

                // Get packages in preinstalled paths that aren't owned by Microsoft.
                var preinstalledPackages = new HashSet<string>();

                foreach (var path in paths)
                {
                    var expandedPath = Environment.ExpandEnvironmentVariables(path);
                    var packagesInPath = Directory.GetDirectories(expandedPath)
                        .Select(d => d.Replace(expandedPath, "").Trim('\\').ToLowerInvariant())
                        .Where(d => !d.StartsWith("."));

                    preinstalledPackages.UnionWith(packagesInPath);
                }

                preinstalledPackages.ExceptWith(microsoftPackages);

                var downloads = context.PackageRegistrations
                    .Where(r => preinstalledPackages.Contains(r.Id))
                    .Select(r => new { Id = r.Id.ToLowerInvariant(), Downloads = r.DownloadCount })
                    .ToList();

                // Get set of dependencies of Microsoft packages and pre-installed packages.
                var microsoftOrPreinstalled = new HashSet<string>(microsoftPackages);
                microsoftOrPreinstalled.UnionWith(preinstalledPackages);

                var dependencies = context.Set<PackageDependency>()
                    .Where(d => !microsoftOrPreinstalled.Contains(d.Id)) // TODO: test case sensitivity
                    .Where(d => microsoftOrPreinstalled.Contains(d.Package.PackageRegistration.Id))
                    .Select(d => d.Id.ToLowerInvariant());

                var dependencyPackages = new HashSet<string>(dependencies);

                // TODO: Insert microsoftPackages, preinstalledPackages, and dependencyPackages into table.
                // For each hash set, order package ids by total downloads. For each package id, get all versions
                // and insert them into database by descending order of versions.
            }

            await Task.Yield();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RepositorySignConfiguration>(configurationRoot.GetSection(JobConfigurationSectionName));

            services.AddScoped<IGalleryContext>(provider =>
            {
                var config = provider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value;

                return new GalleryContext(config.ConnectionString, readOnly: false);
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}