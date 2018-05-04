// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class PackageLagMessage
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public TimeSpan CreatedDelay { get; set; }
        public TimeSpan V3Delay { get; set; }
        public string Region { get; set; }
        public string Subscription { get; set; }
    }
}
