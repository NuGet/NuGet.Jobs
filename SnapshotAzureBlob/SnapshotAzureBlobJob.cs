// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace SnapshotAzureBlob
{
    public class SnapshotAzureBlobJob : JobBase
    {
        private string _connectionString;
        private string _container;

        public ILogger Logger { get; private set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                Logger = loggerFactory.CreateLogger<SnapshotAzureBlobJob>();
                _connectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SnapshotAzureBlobJob_ConnectionString);
                _container = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SnapshotAzureBlobJob_Container);
            }
            catch (Exception exception)
            {
                Logger.LogCritical(LogEvents.JobInitFailed, exception, "Failed to initialize job!");
                return false;
            }
            return true;
        }

        public string GetUsage()
        {
            return "Usage: SnapshotAzureBlobJob "
                   + $"-{JobArgumentNames.SnapshotAzureBlobJob_ConnectionString} <connectionString> "
                   + $"-{JobArgumentNames.SnapshotAzureBlobJob_Container} <container> ";
        }

        public override Task<bool> Run()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            EnsureOneSnapshot(_container, blobClient);
            return Task.FromResult(true);
        }

        private void EnsureOneSnapshot(string containerName, CloudBlobClient client)
        {
            CloudBlobContainer container = client.GetContainerReference(containerName);
            var blobList = container.ListBlobs(prefix: null, useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None);
            int snapshotCount = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Parallel.ForEach(blobList, (item) =>
            {
                try
                {
                    CloudBlockBlob blob = item as CloudBlockBlob;
                    if (blob != null)
                    {
                        //because the query is filtered by the blob prefix
                        //the list count will be bounded by the count of blobs snapshots taken; 
                        //this count is expected to be small
                        var expandedList = container.ListBlobs(prefix: blob.Name, 
                                                               useFlatBlobListing: true,
                                                               blobListingDetails: BlobListingDetails.Snapshots).ToList();
                        if (expandedList.Count() == 1)
                        {
                            Interlocked.Increment(ref snapshotCount);
                            blob.Snapshot();
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogCritical(LogEvents.SnaphotFailed, ex, "The snapshot failed for blob {Blob}.", item.Uri);
                }
            });
            sw.Stop();

            this.Logger.LogInformation("Created {snapshotCount} snapshots in {timeInMilliseconds} milliseconds", snapshotCount, sw.ElapsedMilliseconds);
        }
    }
}
