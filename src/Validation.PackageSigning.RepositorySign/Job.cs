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

namespace Validation.PackageSigning.RepositorySign
{
    using GalleryContext = NuGetGallery.EntitiesContext;
    using IGalleryContext = NuGetGallery.IEntitiesContext;

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
                "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackagesFallback",
                "%USERPROFILE%\\.dotnet\\NuGetFallbackFolder",
                "%USERPROFILE%\\.dotnet\\NuGetFallbackFolder\\.tools"
            };

            foreach (var path in paths)
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(path);

                var packageIdentifiers = Directory.GetDirectories(expandedPath).Where(p => p.StartsWith(".");
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IGalleryContext>();

                var microsoftPackages = context.PackageRegistrations
                    .Where(r => r.Owners.Any(o => o.Username == "Microsoft"))
                    .ToList();

                
            }
            // TODO, run
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