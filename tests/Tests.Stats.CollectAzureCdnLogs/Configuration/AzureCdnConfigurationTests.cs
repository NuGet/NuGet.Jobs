// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureCdnLogs.Configuration;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs.Configuration
{
    public class AzureCdnConfigurationTests
    {
        public class TheGetAzureCdnPlatformMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ThrowsWhenPlatformIsNullOrEmpty(string platform)
            {
                var configuration = new AzureCdnConfiguration();
                configuration.Platform = platform;

                var ex = Assert.Throws<ArgumentException>(() => configuration.GetAzureCdnPlatform());
                Assert.Equal("Job parameter for Azure CDN Platform is not defined.", ex.Message);
            }

            [Theory]
            [InlineData("HttpLargeObject")]
            [InlineData("HttpSmallObject")]
            [InlineData("ApplicationDeliveryNetwork")]
            [InlineData("FlashMediaStreaming")]
            public void ReturnsEnumValueWhenValidPlatformValueProvided(string platform)
            {
                var configuration = new AzureCdnConfiguration();
                configuration.Platform = platform;
                var expected = Enum.Parse(typeof(AzureCdnPlatform), platform);

                var actual = configuration.GetAzureCdnPlatform();
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void ThrowsWhenInvalidPlatformValueProvided()
            {
                var configuration = new AzureCdnConfiguration();
                configuration.Platform = "UnknownPlatform";

                var ex = Assert.Throws<ArgumentException>(() => configuration.GetAzureCdnPlatform());
                Assert.Equal(
                    "Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.", 
                    ex.Message);
            }
        }

        public class TheGetAzureCloudStorageAccountMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ThrowsWhenCloudStorageAccountIsNullOrEmpty(string cloudStorageAccount)
            {
                var configuration = new AzureCdnConfiguration();
                configuration.CloudStorageAccount = cloudStorageAccount;

                var ex = Assert.Throws<ArgumentException>(() => configuration.GetAzureCloudStorageAccount());
                Assert.Equal("Job parameter for Azure CDN Cloud Storage Account is not defined.", ex.Message);
            }

            [Fact]
            public void ReturnsParsedCloudStorageAccountWhenValidValueProvided()
            {
                var configuration = new AzureCdnConfiguration();
                configuration.CloudStorageAccount = "UseDevelopmentStorage=true";

                var actual = configuration.GetAzureCloudStorageAccount();
                Assert.Equal(CloudStorageAccount.DevelopmentStorageAccount, actual);
            }

            [Fact]
            public void ThrowsWhenInvalidCloudStorageAccountValueProvided()
            {
                var configuration = new AzureCdnConfiguration();
                configuration.CloudStorageAccount = "InvalidCloudStorageAccount";

                var ex = Assert.Throws<ArgumentException>(() => configuration.GetAzureCloudStorageAccount());
                Assert.Equal("Job parameter for Azure CDN Cloud Storage Account is invalid.", ex.Message);
            }
        }
    }
}
