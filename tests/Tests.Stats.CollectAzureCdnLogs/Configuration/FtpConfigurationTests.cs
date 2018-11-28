// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.CollectAzureCdnLogs.Configuration;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs.Configuration
{
    public class FtpConfigurationTests
    {
        public class TheGetServerUriMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ThrowsWhenServerUrlIsNullOrEmpty(string serverUrl)
            {
                var configuration = new FtpConfiguration();
                configuration.ServerUrl = serverUrl;

                var ex = Assert.Throws<ArgumentException>(() => configuration.GetServerUri());
                Assert.Contains("FTP Server Url is null or empty.", ex.Message);
            }

            [Fact]
            public void ThrowsWhenServerUrlIsNotUsingFtpScheme()
            {
                var configuration = new FtpConfiguration();
                configuration.ServerUrl = @"https://hostname/folder";

                var ex = Assert.Throws<UriFormatException>(() => configuration.GetServerUri());
                Assert.Contains("FTP Server Url must use the 'ftp://' scheme.", ex.Message);
            }

            [Fact]
            public void AssumesFtpSchemeWhenServerUrlDoesNotProvideAScheme()
            {
                var configuration = new FtpConfiguration();
                configuration.ServerUrl = @"hostname/folder";
                var expected = new Uri("ftp://" + configuration.ServerUrl);

                var actual = configuration.GetServerUri();

                Assert.Equal(expected, actual);
            }
        }
    }
}
