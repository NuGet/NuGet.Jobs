﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CollectAzureChinaCDNLogs
{
    public class Job : JsonConfigurationJob
    {
        private const int DefaultExecutionTimeoutInSeconds = 14400; // 4 hours
        private const int MaxFilesToProcess = 4;
        
        private CollectAzureChinaCdnLogsConfiguration _configuration;
        private int _executionTimeoutInSeconds;
        private Collector _chinaCollector;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            InitializeJobConfiguration(_serviceProvider);
        }

        public void InitializeJobConfiguration(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<CollectAzureChinaCdnLogsConfiguration>>().Value;
            _executionTimeoutInSeconds = _configuration.ExecutionTimeoutInSeconds ?? DefaultExecutionTimeoutInSeconds;

            var blobLeaseManager = new AzureBlobLeaseManager(serviceProvider.GetRequiredService<ILogger<AzureBlobLeaseManager>>());

            var source = new AzureStatsLogSource(
                ValidateAzureCloudStorageAccount(_configuration.AzureAccountConnectionStringSource),
                _configuration.AzureContainerNameSource,
                _executionTimeoutInSeconds / MaxFilesToProcess,
                blobLeaseManager,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogSource>>());

            var dest = new AzureStatsLogDestination(
                ValidateAzureCloudStorageAccount(_configuration.AzureAccountConnectionStringDestination),
                _configuration.AzureContainerNameDestination,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogDestination>>());

            _chinaCollector = new ChinaStatsCollector(source, dest, serviceProvider.GetRequiredService<ILogger<ChinaStatsCollector>>());
        }

        public override async Task Run()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(_executionTimeoutInSeconds*1000);
            var aggregateExceptions = await _chinaCollector.TryProcessAsync(maxFileCount: MaxFilesToProcess,
                 fileNameTransform: s => $"{_configuration.DestinationFilePrefix}_{s}",
                 sourceContentType: ContentType.GZip,
                 destinationContentType: ContentType.GZip,
                 token: cts.Token);

            if (aggregateExceptions != null)
            {
                foreach(var ex in aggregateExceptions.InnerExceptions)
                {
                    Logger.LogError(Stats.AzureCdnLogs.Common.LogEvents.JobRunFailed, ex.InnerException, ex.Message);
                }
            }

            if(cts.IsCancellationRequested)
            {
                Logger.LogInformation($"Execution exceeded the timeout of {_executionTimeoutInSeconds} seconds and it was cancelled.");
            }
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<CollectAzureChinaCdnLogsConfiguration>(services, configurationRoot);
        }
    }
}
