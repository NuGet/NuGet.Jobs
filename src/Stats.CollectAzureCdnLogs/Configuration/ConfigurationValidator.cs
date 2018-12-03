// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using Stats.AzureCdnLogs.Common;

namespace Stats.CollectAzureCdnLogs
{
    internal static class ConfigurationValidator
    {
        public static void ValidateJobConfiguration(CollectAzureCdnLogsConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.AzureCdnAccountNumber))
            {
                throw new ArgumentException("Configuration 'AzureCdnAccountNumber' is required", nameof(configuration));
            }

            if (string.IsNullOrEmpty(configuration.AzureCdnCloudStorageContainerName))
            {
                throw new ArgumentException("Configuration 'AzureCdnCloudStorageContainerName' is required", nameof(configuration));
            }

            if (string.IsNullOrEmpty(configuration.FtpSourceUsername))
            {
                throw new ArgumentException("Configuration 'FtpSourceUsername' is required", nameof(configuration));
            }

            if (string.IsNullOrEmpty(configuration.FtpSourcePassword))
            {
                throw new ArgumentException("Configuration 'FtpSourcePassword' is required", nameof(configuration));
            }

            ValidateAzureCloudStorageAccount(configuration.AzureCdnCloudStorageAccount);
            ValidateAzureCdnPlatform(configuration.AzureCdnPlatform);
            ValidateFtpUri(configuration.FtpSourceUri);
        }

        public static Uri ValidateFtpUri(string serverUrl)
        {
            var trimmedServerUrl = (serverUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedServerUrl))
            {
                throw new ArgumentException("FTP Server Uri is null or empty.", "serverUrl");
            }

            // if no protocol was specified assume ftp
            var schemeRegex = new Regex(@"^[a-zA-Z]+://");
            if (!schemeRegex.IsMatch(trimmedServerUrl))
            {
                trimmedServerUrl = string.Concat(@"ftp://", trimmedServerUrl);
            }

            var uri = new Uri(trimmedServerUrl);
            if (!uri.IsAbsoluteUri)
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must be an absolute URI. Value: '{0}'.", trimmedServerUrl));
            }

            // only ftp is supported but we could support others
            if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must use the 'ftp://' scheme. Value: '{0}'.", trimmedServerUrl));
            }

            return uri;
        }

        public static AzureCdnPlatform ValidateAzureCdnPlatform(string azureCdnPlatform)
        {
            if (string.IsNullOrEmpty(azureCdnPlatform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            if (Enum.TryParse(azureCdnPlatform, true, out AzureCdnPlatform value))
            {
                return value;
            }

            throw new ArgumentException($"Job parameter for Azure CDN Platform is invalid. Allowed values are: {string.Join(", ", Enum.GetValues(typeof(AzureCdnPlatform)))}.");
        }

        public static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            if (CloudStorageAccount.TryParse(cloudStorageAccount, out CloudStorageAccount account))
            {
                return account;
            }

            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }
    }
}
