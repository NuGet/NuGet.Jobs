﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Jobs;
using NuGet.Services.Logging;
using Stats.AzureCdnLogs.Common;
using NuGet.Services.KeyVault;

namespace Stats.ImportAzureCdnStatistics
{
    public class Job
        : JobBase
    {
        private bool _aggregatesOnly;
        private string _azureCdnAccountNumber;
        private string _cloudStorageContainerName;
        private AzureCdnPlatform _azureCdnPlatform;
        private SqlConnectionStringBuilder _targetDatabase;
        private CloudStorageAccount _cloudStorageAccount;
        private CloudBlobClient _cloudBlobClient;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private LogFileProvider _blobLeaseManager;

        public override async Task<bool> Init(IArgumentsDictionary jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = await jobArgsDictionary.GetOrDefault<string>(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                _loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                _logger = _loggerFactory.CreateLogger<Job>();

                var azureCdnPlatform = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.AzureCdnPlatform);
                var cloudStorageAccountConnectionString = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.AzureCdnCloudStorageAccount);
                var databaseConnectionString = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.StatisticsDatabase);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);

                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
                _azureCdnAccountNumber = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.AzureCdnAccountNumber);
                _azureCdnPlatform = ValidateAzureCdnPlatform(azureCdnPlatform);
                _cloudStorageContainerName = ValidateAzureContainerName(await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.AzureCdnCloudStorageContainerName));

                _aggregatesOnly = await jobArgsDictionary.GetOrDefault<bool>(JobArgumentNames.AggregatesOnly);

                // construct a cloud blob client for the configured storage account
                _cloudBlobClient = _cloudStorageAccount.CreateCloudBlobClient();
                _cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

                // Get the source blob container (containing compressed log files)
                // and construct a log source (fetching raw logs from the source blob container)
                var sourceBlobContainer = _cloudBlobClient.GetContainerReference(_cloudStorageContainerName);
                _blobLeaseManager = new LogFileProvider(sourceBlobContainer, _loggerFactory);
            }
            catch (Exception exception)
            {
                _logger?.LogCritical("Failed to initialize job! {Exception}", exception);

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                // Get the target blob container (for archiving decompressed log files)
                var targetBlobContainer = _cloudBlobClient.GetContainerReference(_cloudStorageContainerName + "-archive");
                await targetBlobContainer.CreateIfNotExistsAsync();

                // Get the dead-letter table (corrupted or failed blobs will end up there)
                var deadLetterBlobContainer = _cloudBlobClient.GetContainerReference(_cloudStorageContainerName + "-deadletter");
                await deadLetterBlobContainer.CreateIfNotExistsAsync();

                // Create a parser
                var warehouse = new Warehouse(_loggerFactory, _targetDatabase);
                var logProcessor = new LogFileProcessor(targetBlobContainer, deadLetterBlobContainer, _loggerFactory, warehouse);

                // Get the next to-be-processed raw log file using the cdn raw log file name prefix
                var prefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_", _azureCdnPlatform.GetRawLogFilePrefix(), _azureCdnAccountNumber);

                // Get next raw log file to be processed
                IReadOnlyCollection<string> alreadyAggregatedLogFiles = null;
                if (_aggregatesOnly)
                {
                    // We only want to process aggregates for the log files.
                    // Get the list of files we already processed so we can skip them.
                    alreadyAggregatedLogFiles = await warehouse.GetAlreadyAggregatedLogFilesAsync();
                }

                var leasedLogFiles = await _blobLeaseManager.LeaseNextLogFilesToBeProcessedAsync(prefix, alreadyAggregatedLogFiles);
                foreach (var leasedLogFile in leasedLogFiles)
                {
                    var packageTranslator = new PackageTranslator("packagetranslations.json");
                    var packageStatisticsParser = new PackageStatisticsParser(packageTranslator);
                    await logProcessor.ProcessLogFileAsync(leasedLogFile, packageStatisticsParser, _aggregatesOnly);

                    if (_aggregatesOnly)
                    {
                        _blobLeaseManager.TrackLastProcessedBlobUri(leasedLogFile.Uri);
                    }

                    leasedLogFile.Dispose();
                }
            }
            catch (Exception exception)
            {
                _logger.LogCritical("Job run failed! {Exception}", exception);

                return false;
            }

            return true;
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
    }
}