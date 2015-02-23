using Dapper;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Stats.CalculateTotals
{
    public class Job : JobBase
    {
        private static readonly JobEventSource JobEventSourceLog = JobEventSource.Log;

        private const string _targetBlobName = "totals.json";
        private const string _targetContainerName = "v3-stats0";
        
        public Job() : base(JobEventSource.Log) { }

        private CloudStorageAccount ContentAccount { get; set; }

        private SqlConnectionStringBuilder PackageDatabase { get; set; }

        private SqlConnectionStringBuilder WarehouseDatabase { get; set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                PackageDatabase =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.PackageDatabase,
                            EnvironmentVariableKeys.SqlGallery));

                WarehouseDatabase =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.DestinationDatabase,
                            EnvironmentVariableKeys.SqlWarehouse));

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
                    totals = (await connection.QueryAsync<Totals>(Sql.SqlGetStatistics)).SingleOrDefault();
                }

                if (totals == null)
                {
                    throw new Exception("Failed to get the Totals from the query -- no records were returned..");
                }
                JobEventSourceLog.FinishedQuery(totals.UniquePackages, totals.TotalPackages, totals.Downloads, totals.LastUpdateDateUtc);


                List<OperationTotal> operationTotals;
                JobEventSourceLog.BeginningQuery(WarehouseDatabase.DataSource, WarehouseDatabase.InitialCatalog);
                using (var connection = await WarehouseDatabase.ConnectTo())
                {
                    operationTotals = (await connection.QueryAsync<OperationTotal>(Sql.SqlGetOperationsStatistics)).ToList();
                }

                if (!operationTotals.Any())
                {
                    throw new Exception("Failed to get the OperationTotals from the query -- no records were returned..");
                }

                JobEventSourceLog.FinishedWarehouseQuery(string.Join(", ", operationTotals.Select(t => string.Format("{0}: {1} ({2})", t.Operation, t.Total, t.HourOfOperation))));

                totals.OperationTotals = operationTotals;

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
            public List<OperationTotal> OperationTotals { get; set; }

            public DateTime LastUpdateDateUtc { get { return DateTime.UtcNow; } }

            public string ToJsonLd()
            {
                try
                {
                    var json = JObject.FromObject(this, new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                    var type = new Uri("http://schema.nuget.org/schema#Stats");
                    JObject frame = JsonLdHelper.GetContext("context.Stats.json", type);
                    
                    json.Merge(frame);

                    return json.ToString();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                return string.Empty;
            }
        }

        public class OperationTotal
        {
            public string Operation { get; set; }
            public long Total { get; set; }
            public DateTime HourOfOperation { get; set; }
        }
    }
}
