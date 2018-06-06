// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using GalleryContext = EntitiesContext;
    using IGalleryContext = IEntitiesContext;

    public class Job : JsonConfigurationJob
    {
        private const string InitializeArgumentName = "Initialize";
        private const string JobConfigurationSectionName = "RevalidateJob";

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
                    // TODO: https://github.com/NuGet/Engineering/issues/1443
                    throw new NotImplementedException();
                }
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(JobConfigurationSectionName));
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.Initialization);

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