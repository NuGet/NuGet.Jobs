using System;

namespace NuGet.Jobs.Montoring.PackageLag.Telemetry
{
    public interface ITelemetryService
    {
        void TrackPackageCreationLag(DateTimeOffset eventTime, string packageId, string packageVersion, TimeSpan createdDelay);
        void TrackV3Lag(DateTimeOffset eventTime, string packageId, string packageVersion, TimeSpan v3Delay);
    }
}
