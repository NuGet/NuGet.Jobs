// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Stats.AggregateDownloadsInTempTable
{
    [EventSource(Name = "NetFoundation-NuGet-Jobs-AggregateTotalPackageDownloadsInTempTable")]
    public class JobEventSource
        : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource()
        {
        }
    }
}