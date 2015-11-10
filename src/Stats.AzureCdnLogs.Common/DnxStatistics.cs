// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public class DnxStatistics
        : ITrackUserAgent, ITrackEdgeServerIpAddress
    {
        public string Path { get; set; }
        public string UserAgent { get; set; }
        public DateTime EdgeServerTimeDelivered { get; set; }
        public string EdgeServerIpAddress { get; set; }
        public string DnxVersion { get; set; }
        public string OperatingSystem { get; set; }
        public string FileName { get; set; }
    }
}