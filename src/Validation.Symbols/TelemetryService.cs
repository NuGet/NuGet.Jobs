// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace Validation.Symbols
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "SymbolValidatorJob";
        private const string PackageNotFound = Prefix + "PackageNotFound";
        private const string SymbolNotFound = Prefix + "SymbolNotFound";
        private const string SymbolValidationDuration = Prefix + "SymbolValidationDurationInSeconds";
        private const string MessageDeliveryLag = Prefix + "MessageDeliveryLag";
        private const string MessageEnqueueLag = Prefix + "MessageEnqueueLag";

        private const string PackageId = "PackageId";
        private const string PackageNormalizedVersion = "PackageNormalizedVersion";
        private const string MessageType = "MessageType";
        private const string ExecutionTimeSeconds = "ExecutionTimeSeconds";
        private const string SymbolCount = "SymbolCount";

        private readonly TelemetryClient _telemetryClient;

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackPackageNotFoundEvent(string packageId, string packageNormalizedVersion)
        {
            _telemetryClient.TrackMetric(
                PackageNotFound,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion }
                });
        }

        public void TrackSymbolNotFoundEvent(string packageId, string packageNormalizedVersion)
        {
            _telemetryClient.TrackMetric(
                SymbolNotFound,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion }
                });
        }

        public void TrackSymbolValidationDurationEvent(string packageId, string packageNormalizedVersion, long executiontimeInSeconds, int symbolCount)
        {
            _telemetryClient.TrackMetric(
                SymbolValidationDuration,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { PackageNormalizedVersion, packageNormalizedVersion },
                    { ExecutionTimeSeconds, executiontimeInSeconds.ToString()},
                    { SymbolCount, symbolCount.ToString()}
                });
        }

        public void TrackMessageDeliveryLag<TMessage>(TimeSpan deliveryLag)
            => _telemetryClient.TrackMetric(
                MessageDeliveryLag,
                deliveryLag.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name }
                });

        public void TrackEnqueueLag<TMessage>(TimeSpan enqueueLag)
            => _telemetryClient.TrackMetric(
                MessageEnqueueLag,
                enqueueLag.TotalSeconds,
                new Dictionary<string, string>
                {
                    { MessageType, typeof(TMessage).Name }
                });
    }
}
