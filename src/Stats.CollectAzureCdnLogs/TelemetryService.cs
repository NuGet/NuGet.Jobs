// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using NuGet.Services.Logging;

namespace Stats.CollectAzureCdnLogs
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "Stats.CollectAzureCdnLogs.";
        private const string FileNamePropertyName = "FileName";
        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackRawLogCount(int count)
        {
            _telemetryClient.TrackMetric(Prefix + "RawLogCount", count);
        }

        public void TrackProcessedBlob(string fileName, long compressedBytes, int lineCount)
        {
            var properties = new Dictionary<string, string> { { FileNamePropertyName, fileName } };
            _telemetryClient.TrackMetric(Prefix + "LogCompressedSourceFileSizeBytes", compressedBytes, properties);
            _telemetryClient.TrackMetric(Prefix + "LogLineCount", lineCount, properties);
        }

        public IDisposable TrackLogProcessingDuration(string fileName)
            => _telemetryClient.TrackDuration(Prefix + "LogProcessingTimeSeconds", new Dictionary<string, string>
                {
                    { FileNamePropertyName, fileName },
                });
    }
}