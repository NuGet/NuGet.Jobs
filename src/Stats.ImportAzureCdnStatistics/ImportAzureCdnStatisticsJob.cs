﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class ImportAzureCdnStatisticsJob : JsonConfigurationJob
    {
        private ImportAzureCdnStatisticsConfiguration Configuration { get; set; }

        private AzureCdnPlatform AzureCdnPlatform { get; set; }

        public CloudBlobClient CloudBlobClient { get; set; }

        private LogFileProvider BlobLeaseManager { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            Configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<ImportAzureCdnStatisticsConfiguration>>().Value;

            AzureCdnPlatform = ValidateAzureCdnPlatform(Configuration.AzureCdnPlatform);

            var cloudStorageAccount = ValidateAzureCloudStorageAccount(Configuration.AzureCdnCloudStorageAccount);
            CloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            BlobLeaseManager = new LogFileProvider(
                CloudBlobClient.GetContainerReference(Configuration.AzureCdnCloudStorageContainerName),
                LoggerFactory);
        }

        public override async Task Run()
        {
            // Get the target blob container (for archiving decompressed log files)
            var targetBlobContainer = CloudBlobClient.GetContainerReference(
                Configuration.AzureCdnCloudStorageContainerName + "-archive");
            await targetBlobContainer.CreateIfNotExistsAsync();

            // Get the dead-letter table (corrupted or failed blobs will end up there)
            var deadLetterBlobContainer = CloudBlobClient.GetContainerReference(
                Configuration.AzureCdnCloudStorageContainerName + "-deadletter");
            await deadLetterBlobContainer.CreateIfNotExistsAsync();

            // Create a parser
            var warehouse = new Warehouse(LoggerFactory, OpenSqlConnectionAsync<StatisticsDbConfiguration>);
            var statisticsBlobContainerUtility = new StatisticsBlobContainerUtility(
                targetBlobContainer,
                deadLetterBlobContainer,
                LoggerFactory);

            var logProcessor = new LogFileProcessor(statisticsBlobContainerUtility, LoggerFactory, warehouse);

            // Get the next to-be-processed raw log file using the cdn raw log file name prefix
            var prefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_",
                AzureCdnPlatform.GetRawLogFilePrefix(),
                Configuration.AzureCdnAccountNumber);

            // Get next raw log file to be processed
            IReadOnlyCollection<string> alreadyAggregatedLogFiles = null;
            if (Configuration.AggregatesOnly)
            {
                // We only want to process aggregates for the log files.
                // Get the list of files we already processed so we can skip them.
                alreadyAggregatedLogFiles = await warehouse.GetAlreadyAggregatedLogFilesAsync();
            }

            var leasedLogFiles = await BlobLeaseManager.LeaseNextLogFilesToBeProcessedAsync(prefix, alreadyAggregatedLogFiles);
            foreach (var leasedLogFile in leasedLogFiles)
            {
                var packageTranslator = new PackageTranslator("packagetranslations.json");
                var packageStatisticsParser = new PackageStatisticsParser(packageTranslator, LoggerFactory);
                await logProcessor.ProcessLogFileAsync(leasedLogFile, packageStatisticsParser, Configuration.AggregatesOnly);

                if (Configuration.AggregatesOnly)
                {
                    BlobLeaseManager.TrackLastProcessedBlobUri(leasedLogFile.Uri);
                }

                leasedLogFile.Dispose();
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

        private static AzureCdnPlatform ValidateAzureCdnPlatform(string azureCdnPlatform)
        {
            if (string.IsNullOrEmpty(azureCdnPlatform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            AzureCdnPlatform value;
            if (Enum.TryParse(azureCdnPlatform, true, out value))
            {
                return value;
            }
            throw new ArgumentException("Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.");
        }

        private static string ValidateAzureContainerName(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Container Name is not defined.");
            }
            return containerName;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<ImportAzureCdnStatisticsConfiguration>(services, configurationRoot);
        }
    }
}