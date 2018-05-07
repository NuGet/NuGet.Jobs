using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Options;
using NuGet.Services.Logging;
using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Montoring.PackageLag.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryClient _telemetryClient;

        private const string PackageId = "PackageId";
        private const string PackageVersion = "Version";
        private const string Region = "Region";
        private const string Subscrption = "Subscription";

        private const string CreatedLagName = "PackageCreationLagInSeconds";
        private const string V3LagName = "V3LagInSeconds";

        private readonly string _region;
        private readonly string _subscription;

        public TelemetryService(ITelemetryClient telemetryClient, PackageLagMonitorConfiguration configuration)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _region = configuration.Region ?? "";
            _subscription = configuration.Subscription ?? "";
        }

        public void TrackPackageCreationLag(DateTimeOffset eventTime, string packageId, string packageVersion, TimeSpan createdDelay)
        {
            _telemetryClient.TrackMetric(CreatedLagName, createdDelay.TotalSeconds, new Dictionary<string, string>
            {
                { PackageId, packageId },
                { PackageVersion, packageVersion },
                { Region, _region },
                { Subscrption, _subscription }
            });
        }

        public void TrackV3Lag(DateTimeOffset eventTime, string packageId, string packageVersion, TimeSpan v3Delay)
        {
            _telemetryClient.TrackMetric(CreatedLagName, v3Delay.TotalSeconds, new Dictionary<string, string>
            {
                { PackageId, packageId },
                { PackageVersion, packageVersion },
                { Region, _region },
                { Subscrption, _subscription }
            });
        }
    }
}
