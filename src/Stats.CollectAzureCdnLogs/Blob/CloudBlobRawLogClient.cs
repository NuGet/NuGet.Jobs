// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Stats.CollectAzureCdnLogs.Configuration;

namespace Stats.CollectAzureCdnLogs.Blob
{
    internal sealed class CloudBlobRawLogClient
    {
        private readonly ILogger _logger;
        private readonly AzureCdnConfiguration _azureCdnConfiguration;

        public CloudBlobRawLogClient(
            ILogger<CloudBlobRawLogClient> logger, 
            AzureCdnConfiguration azureCdnConfiguration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azureCdnConfiguration = azureCdnConfiguration ?? throw new ArgumentNullException(nameof(azureCdnConfiguration));
        }

        public async Task<CloudBlobContainer> CreateContainerIfNotExistsAsync()
        {
            var cloudStorageAccount = _azureCdnConfiguration.GetAzureCloudStorageAccount();
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            var cloudBlobContainer = cloudBlobClient.GetContainerReference(_azureCdnConfiguration.CloudStorageContainerName);
            await cloudBlobContainer.CreateIfNotExistsAsync();

            return cloudBlobContainer;
        }

        public async Task<CloudBlobStream> OpenBlobForWriteAsync(CloudBlobContainer targetContainer, RawLogFileInfo logFile, string fileName)
        {
            if (targetContainer == null)
            {
                throw new ArgumentNullException(nameof(targetContainer));
            }
            if (logFile == null)
            {
                throw new ArgumentNullException(nameof(logFile));
            }

            var blobName = logFile.Uri.ToString();

            _logger.LogInformation("Beginning blob upload: '{BlobUri}'", blobName);

            var blob = targetContainer.GetBlockBlobReference(fileName);
            blob.Properties.ContentType = logFile.ContentType;

            // return a writeable stream
            return await blob.OpenWriteAsync();
        }

        public async Task<bool> CheckIfBlobExistsAsync(CloudBlobContainer targetContainer, string fileName)
        {
            using (_logger.BeginScope("Checking if file '{FileName}' exists.", fileName))
            {
                try
                {
                    var blob = targetContainer.GetBlockBlobReference(fileName);
                    var exists = await blob.ExistsAsync();

                    _logger.LogInformation("Finished checking if file '{FileName}' exists (exists = {FileExists}).", fileName, exists);

                    return exists;
                }
                catch (Exception exception)
                {
                    _logger.LogError("Failed to check if file exists. {Exception}", exception);
                }
            }

            return false;
        }
    }
}