using System;
using System.Diagnostics.Tracing;

namespace Stats.CalculateTotals
{
    [EventSource(Name = "Outercurve-NuGet-Jobs-CalculateStatsTotals")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();
        private JobEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Begining the query of the database to get statistics from {0}/{1}",
            Task = Tasks.Querying,
            Opcode = EventOpcode.Start)]
        public void BeginningQuery(string server, string database) { WriteEvent(1, server, database); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Message = "Finished querying the database. Unique Packages: {0}, Total Packages: {1}, Download Count: {2}, Last Updated Date UTC: {3}",
            Task = Tasks.Querying,
            Opcode = EventOpcode.Stop)]
        public void FinishedQuery(int uniquePackages, int totalPackages, long downloadCount, DateTime lastUpdatedUtc)
        {
            WriteEvent(2, uniquePackages, totalPackages, downloadCount, lastUpdatedUtc);
        }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "Finished querying the warehouse. {0}",
            Task = Tasks.Querying,
            Opcode = EventOpcode.Stop)]
        public void FinishedWarehouseQuery(string result)
        {
            WriteEvent(3, result);
        }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Beginning blob upload: {0}",
            Task = Tasks.Uploading,
            Opcode = EventOpcode.Start)]
        public void BeginningBlobUpload(string blobName) { WriteEvent(4, blobName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Message = "Finished blob upload",
            Task = Tasks.Uploading,
            Opcode = EventOpcode.Stop)]
        public void FinishedBlobUpload() { WriteEvent(5); }

        public static class Tasks
        {
            public const EventTask Querying = (EventTask)0x1;
            public const EventTask Uploading = (EventTask)0x2;
        }
    }
}