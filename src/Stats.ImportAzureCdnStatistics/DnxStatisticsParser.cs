// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class DnxStatisticsParser
        : StatisticsParser
    {
        public static DnxStatistics FromCdnLogEntry(CdnLogEntry logEntry)
        {
            var statistics = GetDnxStatisticsFromRequestUrl(logEntry.RequestUrl, logEntry.EdgeServerTimeDelivered);
            if (statistics != null)
            {
                statistics.EdgeServerIpAddress = logEntry.EdgeServerIpAddress;
                statistics.UserAgent = GetUserAgentValue(logEntry);
            }
            return statistics;
        }

        public static DnxStatistics GetDnxStatisticsFromRequestUrl(string requestUrl, DateTime edgeServerTimeDelivered)
        {
            var matches = Regex.Matches(requestUrl, @"(http[s]?[:]//dist.asp.net/[\w]*/distaspnet.blob.core.windows.net/assets/installer/?)(?<dnxVersion>[a-zA-Z0-9.-]+)/(?<os>[\w]+)/(?<fileName>[a-zA-Z0-9.-]+)");
            if (matches.Count == 1)
            {
                var match = matches[0];
                var statistics = new DnxStatistics();
                statistics.EdgeServerTimeDelivered = edgeServerTimeDelivered;

                statistics.DnxVersion = match.Groups["dnxVersion"].Value.Trim();
                statistics.OperatingSystem = match.Groups["os"].Value.Trim();
                statistics.FileName = match.Groups["fileName"].Value.Trim();
                statistics.Path = string.Join("/", statistics.DnxVersion, statistics.OperatingSystem, statistics.FileName);

                return statistics;
            }
            return null;
        }
    }
}