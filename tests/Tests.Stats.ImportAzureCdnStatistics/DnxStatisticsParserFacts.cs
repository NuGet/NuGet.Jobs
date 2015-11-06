// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class DnxStatisticsParserFacts
    {
        [Theory]
        [InlineData("http://dist.asp.net/80E977/distaspnet.blob.core.windows.net/assets/installer/1.0.0-rc1-15838/linux/dnx-coreclr-linux-x64.1.0.0-rc1-15838.tar.gz", "1.0.0-rc1-15838", "linux", "dnx-coreclr-linux-x64.1.0.0-rc1-15838.tar.gz")]
        [InlineData("https://dist.asp.net/80E977/distaspnet.blob.core.windows.net/assets/installer/1.0.0-beta8/linux/dnx-coreclr-linux-x64.1.0.0-beta8.tar.gz", "1.0.0-beta8", "linux", "dnx-coreclr-linux-x64.1.0.0-beta8.tar.gz")]
        [InlineData("http://dist.asp.net/80E977/distaspnet.blob.core.windows.net/assets/installer/1.0.0-beta8/win/aspnet.exe", "1.0.0-beta8", "win", "aspnet.exe")]
        public void GetDnxStatisticsFromRequestUrl(string requestUrl, string dnxVersion, string operatingSystem, string fileName)
        {
            var toolInfo = ToolStatisticsParser.GetToolStatisticsFromRequestUrl(requestUrl, DateTime.UtcNow);
            var dnxInfo = DnxStatisticsParser.GetDnxStatisticsFromRequestUrl(requestUrl, DateTime.UtcNow);

            Assert.Equal(dnxVersion, dnxInfo.DnxVersion);
            Assert.Equal(operatingSystem, dnxInfo.OperatingSystem);
            Assert.Equal(fileName, dnxInfo.FileName);
            Assert.Equal(string.Join("/", dnxVersion, operatingSystem, fileName), dnxInfo.Path);
        }
    }
}
