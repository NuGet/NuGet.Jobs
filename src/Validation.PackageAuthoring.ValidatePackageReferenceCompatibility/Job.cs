// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.ServiceBus;
using Validation.PackageCompatibility.Core.Messages;

namespace Validation.PackageCompatibility.PackageReferenceValidation
{
    internal class Job : JobBase
    {
        /// <summary>
        /// The argument this job uses to determine the configuration file's path.
        /// </summary>
        private const string ConfigurationArgument = "Configuration";

        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";

        /// <summary>
        /// The maximum time that a KeyVault secret will be cached for.
        /// </summary>
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        /// <summary>
        /// The maximum amount of time that graceful shutdown can take before the job will
        /// forcefully end itself.
        /// </summary>
        private static readonly TimeSpan MaxShutdownTime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The configured service provider, used to instiate the services this job depends on.
        /// </summary>
        private IServiceProvider _serviceProvider;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));

        }

        public async override Task Run()
        {
            // I am understanding this correctly?
            // All these run get triggered on a validation message?
            // If that true, this message would need serialization code, sent by someone etc.
            var processor = _serviceProvider.GetRequiredService<ISubscriptionProcessor<PackageCompatibilityValidationMessage>>();

            processor.Start();

            // Wait a day, and then shutdown this process so that it is restarted.
            await Task.Delay(TimeSpan.FromDays(1));

            if (!await processor.ShutdownAsync(MaxShutdownTime))
            {
                Logger.LogWarning(
                    "Failed to gracefully shutdown Service Bus subscription processor. {MessagesInProgress} messages left",
                    processor.NumberOfMessagesInProgress);
            }
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();

            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        // TODO NK - Why are these configuration root methods all the same, yet not common? DRY
        // This loooks pretty generic
        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // Use the custom NonCachingOptionsSnapshot so that KeyVault secret injection works properly.
            //services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            //services.AddSingleton(LoggerFactory);
            //services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));

            //services.AddScoped<IValidationEntitiesContext>(p =>
            //{
            //    var config = p.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>().Value;

            //    return new ValidationEntitiesContext(config.ConnectionString);
            //});

            //services.AddTransient<ISubscriptionClient>(p =>
            //{
            //    var config = p.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

            //    return new SubscriptionClientWrapper(config.ConnectionString, config.TopicPath, config.SubscriptionName);
            //});
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            throw new NotImplementedException();
        }
    }
}
