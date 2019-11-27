// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.Cursor;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.Storage;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string MonitorConfigurationSectionName = "MonitorConfiguration";

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private IServiceProvider _serviceProvider;
        private ReadCursor<DateTimeOffset> _cursor;
        private IGitHubVulnerabilitiesLagTelemetryService _telemetryService;
        private IGitHubQueryService _queryService;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);

            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));
            _cursor = _serviceProvider.GetRequiredService<ReadCursor<DateTimeOffset>>();
            _telemetryService = _serviceProvider.GetRequiredService<IGitHubVulnerabilitiesLagTelemetryService>();
            _queryService = _serviceProvider.GetRequiredService<IGitHubQueryService>();
        }

        public async override Task Run()
        {
            var token = CancellationToken.None;

            try
            {
                var currentTimeUtc = DateTimeOffset.UtcNow;

                // Query for the current cursor of GitHubVulnerabilities2Db job.
                // We'll use this cursor to check for newer results in GitHub's API.
                await _cursor.Load(token);

                // Query GitHub for the latest security advisory UpdatedAt value beyond the current job's cursor position.
                var latestAdvisoryUpdatedAt = await _queryService.GetLatestAdvisoryUpdateAsync(_cursor.Value, token);

                // Track metric
                _telemetryService.TrackGitHubVulnerabilities2DbLag(currentTimeUtc, _cursor.Value, latestAdvisoryUpdatedAt);
            }
            catch (Exception e)
            {
                Logger.LogError("Could not track GitHub vulnerabilities lag due to exception. {Exception}", e);
            }
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();
            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private static void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<MonitoringConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));
            services.AddSingleton(p => new HttpClient());
            services.AddTransient<IGitHubVulnerabilitiesLagTelemetryService, GitHubVulnerabilitiesLagTelemetryService>();
            services.AddSingleton(new TelemetryClient());
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<MonitoringConfiguration>(
                p => p.GetRequiredService<IOptionsSnapshot<MonitoringConfiguration>>().Value);
            services.AddTransient<IGitHubQueryService, GitHubQueryService>();

            services.AddTransient<CloudStorageAccount>(p =>
            {
                var configuration = p.GetRequiredService<MonitoringConfiguration>();
                return CloudStorageAccount.Parse(configuration.StorageConnectionString);
            });
            services.AddTransient<IStorageFactory>(p =>
            {
                return new AzureStorageFactory(
                    p.GetRequiredService<CloudStorageAccount>(),
                    p.GetRequiredService<MonitoringConfiguration>().CursorContainerName,
                    p.GetRequiredService<ILogger<AzureStorage>>());
            });

            services.AddTransient<ReadCursor<DateTimeOffset>>(p => CreateCursor(
                p.GetRequiredService<MonitoringConfiguration>(),
                p.GetRequiredService<IStorageFactory>()));
        }

        private static DurableCursor CreateCursor(
            MonitoringConfiguration configuration,
            IStorageFactory storageFactory)
        {
            var storage = storageFactory.Create();

            return new DurableCursor(
                storage.ResolveUri(configuration.AdvisoryCursorBlobName),
                storage,
                DateTimeOffset.MinValue);
        }
    }
}
