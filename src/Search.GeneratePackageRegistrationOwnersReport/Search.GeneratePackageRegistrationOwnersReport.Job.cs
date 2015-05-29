using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs.Common;
using NuGet.Versioning;

namespace Stats.GenerateDownloadCount
{
    internal class Job : JobBase
    {
        private const string GetPackageReigstrationOwnersScript = @"-- Work Service / GeneratePackageRegistrationOwnersReport
            SELECT p.[Key] AS PackageKey, pr.Id, p.NormalizedVersion, p.DownloadCount 
            FROM Packages p WITH (NOLOCK)
            INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]";

        public static readonly string DefaultContainerName = "ng-search-data";
        public static readonly string ReportName = "packageregistrationowners.v1.json";


        public SqlConnectionStringBuilder Source { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public CloudBlobContainer DestinationContainer { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        public override async Task<bool> Run()
        {
            return true;
        }

        protected async Task WriteReport(string report, string name, Formatting formatting)
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                await WriteToFile(report, name);
            }
            else
            {
                await DestinationContainer.CreateIfNotExistsAsync();
                await WriteToBlob(report, name);
            }
        }

        private async Task WriteToFile(string report, string name)
        {
            string fullPath = Path.Combine(OutputDirectory, name);
            string parentDir = Path.GetDirectoryName(fullPath);
            Trace.TraceInformation(String.Format("Writing report to {0}", fullPath));

            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            using (var writer = new StreamWriter(File.OpenWrite(fullPath)))
            {
                await writer.WriteAsync(report);
            }

            Trace.TraceInformation(String.Format("Wrote report to {0}", fullPath));
        }

        private async Task WriteToBlob(string report, string name)
        {
            var blob = DestinationContainer.GetBlockBlobReference(name);
            Trace.TraceInformation(String.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "json";
            await blob.UploadTextAsync(report);

            Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            Source =
                new SqlConnectionStringBuilder(
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.SourceDatabase,
                        EnvironmentVariableKeys.SqlGallery));

            OutputDirectory = JobConfigManager.GetArgument(jobArgsDictionary,
                       JobArgumentNames.OutputDirectory);

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Destination = CloudStorageAccount.Parse(
                                           JobConfigManager.GetArgument(jobArgsDictionary,
                                               JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

                DestinationContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultContainerName;


                DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            }

            return true;

        }
    }
}


