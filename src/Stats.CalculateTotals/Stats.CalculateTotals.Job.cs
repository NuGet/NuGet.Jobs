using Dapper;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;

namespace Stats.CalculateTotals
{
    public class Job : JobBase
    {
        private static readonly JobEventSource JobEventSourceLog = JobEventSource.Log;

        private const string _targetBlobName = "stats-totals.json";
        private const string _targetContainerName = "v3-stats0";

        // Note the NOLOCK hints here!
        private const string _sqlGetStatistics = @"SELECT 
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
                            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages,
                    (SELECT TotalDownloadCount FROM GallerySettings WITH (NOLOCK)) AS Downloads,
                    (SELECT -1) AS Installs";
        
        public Job() : base(JobEventSource.Log) { }

        private CloudStorageAccount ContentAccount { get; set; }

        private SqlConnectionStringBuilder PackageDatabase { get; set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                PackageDatabase =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.PackageDatabase,
                            EnvironmentVariableKeys.SqlGallery));

                var storageGalleryCstr = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.StorageGallery);
                if (String.IsNullOrEmpty(storageGalleryCstr))
                {
                    throw new ArgumentException("Environment variable for storage gallery is not defined");
                }

                ContentAccount = CloudStorageAccount.Parse(storageGalleryCstr);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var blobContainer = ContentAccount.CreateCloudBlobClient().GetContainerReference(_targetContainerName);

                Totals totals;
                JobEventSourceLog.BeginningQuery(PackageDatabase.DataSource, PackageDatabase.InitialCatalog);
                using (var connection = await PackageDatabase.ConnectTo())
                {
                    totals = (await connection.QueryAsync<Totals>(_sqlGetStatistics)).SingleOrDefault();
                }

                if (totals == null)
                {
                    throw new Exception("Failed to get the Totals from the query -- no records were returned..");
                }

                JobEventSourceLog.FinishedQuery(totals.UniquePackages, totals.TotalPackages, totals.Downloads, totals.Installs, totals.LastUpdateDateUtc);

                JobEventSourceLog.BeginningBlobUpload(_targetBlobName);
                await StorageHelpers.UploadJsonBlob(blobContainer, _targetBlobName, totals.ToJsonLd());
                JobEventSourceLog.FinishedBlobUpload();

                return true;
            }
            catch(SqlException ex)
            {
                Trace.TraceError(ex.ToString());
            }
            catch (StorageException ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public class Totals
        {
            public int UniquePackages { get; set; }
            public int TotalPackages { get; set; }
            public long Downloads { get; set; }
            public long Installs { get; set; }

            public DateTime LastUpdateDateUtc { get { return DateTime.UtcNow; } }

            public string ToJsonLd()
            {
                return JsonConvert.SerializeObject(this);
            }
        }
    }

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
            Message = "Finished querying the database. Unique Packages: {0}, Total Packages: {1}, Download Count: {2}, Installs Count: {3}, Last Updated Date UTC: {4}",
            Task = Tasks.Querying,
            Opcode = EventOpcode.Stop)]
        public void FinishedQuery(int uniquePackages, int totalPackages, long downloadCount, long installs, DateTime lastUpdatedUtc)
        {
            WriteEvent(2, uniquePackages, totalPackages, downloadCount, installs, lastUpdatedUtc);
        }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "Beginning blob upload: {0}",
            Task = Tasks.Uploading,
            Opcode = EventOpcode.Start)]
        public void BeginningBlobUpload(string blobName) { WriteEvent(3, blobName); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Finished blob upload",
            Task = Tasks.Uploading,
            Opcode = EventOpcode.Stop)]
        public void FinishedBlobUpload() { WriteEvent(4); }

        public static class Tasks
        {
            public const EventTask Querying = (EventTask)0x1;
            public const EventTask Uploading = (EventTask)0x2;
        }
    }
}
