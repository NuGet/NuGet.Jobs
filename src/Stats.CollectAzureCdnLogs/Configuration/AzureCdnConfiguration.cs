// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage;
using Stats.AzureCdnLogs.Common;

namespace Stats.CollectAzureCdnLogs.Configuration
{
    public class AzureCdnConfiguration
    {
        public string AccountNumber { get; set; }
        public string Platform { get; set; }
        public string CloudStorageAccount { get; set; }
        public string CloudStorageContainerName { get; set; }

        public AzureCdnPlatform GetAzureCdnPlatform()
        {
            if (string.IsNullOrEmpty(Platform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            if (Enum.TryParse(Platform, true, out AzureCdnPlatform value))
            {
                return value;
            }

            throw new ArgumentException("Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.");
        }
               
        public CloudStorageAccount GetAzureCloudStorageAccount()
        {
            if (string.IsNullOrEmpty(CloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            if (Microsoft.WindowsAzure.Storage.CloudStorageAccount.TryParse(CloudStorageAccount, out CloudStorageAccount account))
            {
                return account;
            }

            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }
    }
}
