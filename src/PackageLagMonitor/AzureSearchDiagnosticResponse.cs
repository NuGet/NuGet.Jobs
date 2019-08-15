using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class AzureSearchDiagnosticResponse
    {
        public bool Success { get; set; }

        public TimeSpan Duration { get; set; }

        public Server Server { get; set; }

        public IndexInformation SearchIndex { get; set; }

        public IndexInformation HijackIndex { get; set; }

        public AuxiliaryFileInformations AuxiliaryFiles { get; set; }
    }

    public class Server
    {
        public string MachineName { get; set; }

        public long ProcessId { get; set; }

        public DateTimeOffset ProcessStartTime { get; set; }

        public TimeSpan ProcessDuration { get; set; }

        public string DeploymentLabel { get; set; }

        public string AssemblyCommitId { get; set; }

        public string AssemblyInformationVersion { get; set; }

        public DateTimeOffset AssemblyBuildDateUtc { get; set; }

        public string InstanceId { get; set; }
    }

    public class IndexInformation
    {
        public string Name { get; set; }
        
        public long DocumentCount { get; set; }

        public TimeSpan DocumentCountDuration { get; set; }

        public TimeSpan WarmQueryDuration { get; set; }

        public DateTimeOffset LastCommitTimestamp { get; set; }

        public TimeSpan LastCommitTimestampDuration { get; set; }
     }

    public class AuxiliaryFileInformations
    {
        public AuxiliaryFileInformation Downloads { get; set; }

        public AuxiliaryFileInformation VerifiedPackages { get; set; }
    }

    public class AuxiliaryFileInformation
    {
        public DateTimeOffset LastModified { get; set; }

        public DateTimeOffset Loaded { get; set; }

        public TimeSpan LoadDuration { get; set; }

        public long FileSize { get; set; }

        public string ETag { get; set; }
    }
}
