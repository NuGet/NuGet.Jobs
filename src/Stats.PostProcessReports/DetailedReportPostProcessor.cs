// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet.Services.Storage;

namespace Stats.PostProcessReports
{
    public class DetailedReportPostProcessor : IDetailedReportPostProcessor
    {
        //private readonly IStorage _sourceStorage;
        //private readonly IStorage _destinationStorage;
        private readonly CloudStorageAccount _storageAccount;
        private readonly PostProcessReportsConfiguration _configuration;
        private readonly ILogger<DetailedReportPostProcessor> _logger;

        public DetailedReportPostProcessor(
            //IStorage sourceStorage,
            //IStorage destinationStorage,
            CloudStorageAccount cloudStorageAccount,
            IOptionsSnapshot<PostProcessReportsConfiguration> configurationAccessor,
            ILogger<DetailedReportPostProcessor> logger)
        {
            //_sourceStorage = sourceStorage ?? throw new ArgumentNullException(nameof(sourceStorage));
            //_destinationStorage = destinationStorage ?? throw new ArgumentNullException(nameof(destinationStorage));
            _storageAccount = cloudStorageAccount ?? throw new ArgumentNullException(nameof(cloudStorageAccount));
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value ?? throw new ArgumentException($"{nameof(configurationAccessor.Value)} property must not be null", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CopyFilesAsync()
        {
            ServicePointManager.DefaultConnectionLimit = _configuration.ReportWriteDegreeOfParallelism + 10;
            _logger.LogInformation("Connection limit: {ConnectionLimit}", ServicePointManager.DefaultConnectionLimit);
            var blobClient = _storageAccount.CreateCloudBlobClient();
            var sourceContainer = blobClient.GetContainerReference(_configuration.SourceContainerName);
            var destinationContainer = blobClient.GetContainerReference(_configuration.DestinationContainerName);
            List<IListBlobItem> sourceBlobs = await EnumerateSourceBlobsAsync(sourceContainer);

            foreach (var sourceBlob in sourceBlobs)
            {
                _logger.LogInformation("Blob to consider: {BlobUri}", sourceBlob.Uri.AbsoluteUri);
            }

            foreach (var sourceBlob in sourceBlobs)
            {
                var sourceBlobStats = new BlobStatistics();
                var individualReports = await ProcessSourceBlobAsync(sourceBlob, sourceBlobStats);
                var blobName = sourceBlob.Uri.AbsoluteUri.Split('/').Last();
                using (_logger.BeginScope("Processing {BlobName}", blobName))
                {
                    if (individualReports.Any())
                    {
                        var consumerTasks = Enumerable
                            .Range(1, _configuration.ReportWriteDegreeOfParallelism)
                            .Select(instanceId => WriteReports(instanceId, individualReports, destinationContainer, sourceBlobStats, blobName))
                            .ToList();

                        await Task.WhenAll(consumerTasks);
                    }
                }
            }

            _logger.LogInformation("Done processing");
        }

        private async Task<List<IListBlobItem>> EnumerateSourceBlobsAsync(CloudBlobContainer sourceContainer)
        {
            var sourceBlobs = new List<IListBlobItem>();
            string prefix = _configuration.SourcePath + _configuration.DetailedReportDirectoryName + "/";
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                var segment = await sourceContainer.ListBlobsSegmentedAsync(prefix, blobContinuationToken);
                blobContinuationToken = segment.ContinuationToken;
                foreach (var blob in segment.Results.Where(b => b.Uri.AbsolutePath.EndsWith(".json")))
                {
                    sourceBlobs.Add(blob);
                }
            } while (blobContinuationToken != null);
            return sourceBlobs;
        }

        private class PackageIdContainer
        {
            public string PackageId { get; set; }
        };

        private async Task<ConcurrentBag<LineProcessingContext>> ProcessSourceBlobAsync(
            IListBlobItem blob,
            BlobStatistics blobStats)
        {
            var blobPath = blob.Uri.AbsolutePath.Substring(blob.Uri.AbsolutePath.IndexOf('/', 1) + 1); // dropping container from path
            var sourceBlob = blob.Container.GetBlockBlobReference(blobPath);
            _logger.LogInformation("Processing {BlobUrl}", sourceBlob.Uri.AbsoluteUri);
            var sw = Stopwatch.StartNew();
            var numLines = 0;
            var individualReports = new ConcurrentBag<LineProcessingContext>();
            using (var sourceStream = await sourceBlob.OpenReadAsync())
            using (var streamReader = new StreamReader(sourceStream))
            {
                string line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    ++numLines;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    individualReports.Add(new LineProcessingContext
                    {
                        LineNumber = numLines,
                        Data = line,
                    });
                }
            }
            sw.Stop();
            blobStats.TotalLineCount = numLines;
            _logger.LogInformation("Read {NumLines} lines from {BlobUrl} in {Elapsed}",
                numLines,
                sourceBlob.Uri.AbsoluteUri,
                sw.Elapsed);
            return individualReports;
        }

        private async Task WriteReports(
            int instanceId,
            ConcurrentBag<LineProcessingContext> individualReports,
            CloudBlobContainer destinationContainer,
            BlobStatistics blobStats,
            string blobName)
        {
            int numFailures = 0;
            int itemsProcessed = 0;
            _logger.LogInformation("Starting {InstanceId} instance of report writer", instanceId);
            var sw = Stopwatch.StartNew();
            while (individualReports.TryTake(out LineProcessingContext details))
            {
                PackageIdContainer data;
                try
                {
                    data = JsonConvert.DeserializeObject<PackageIdContainer>(details.Data);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Instance {InstanceId} failed to parse line {LineNumber} in {BlobName}: {Line}",
                        instanceId,
                        details.LineNumber,
                        blobName,
                        details.Data);;
                    blobStats.IncrementLinesFailed();
                    ++numFailures;
                    continue;
                }
                var outFilename = $"recentpopularitydetail_{data.PackageId}.json";
                var destinationBlobPath = $"{_configuration.DestinationPath}/{outFilename}";

                var destinationBlob = destinationContainer.GetBlockBlobReference(destinationBlobPath);
                using (var destinationStream = await destinationBlob.OpenWriteAsync())
                using (var streamWriter = new StreamWriter(destinationStream))
                {
                    await streamWriter.WriteLineAsync(details.Data);
                }
                blobStats.IncrementFileCreated();
                if (++itemsProcessed % 100 == 0)
                {
                    _logger.LogInformation("Processed {NumItems} items, got {NumFailures} failures to parse in {Elapsed} by {Instance}",
                        itemsProcessed,
                        numFailures,
                        sw.Elapsed,
                        instanceId);
                }
            }
            sw.Stop();
            _logger.LogInformation("Instance {Instance} done processing {BlobName}. Processed {NumItems} items, got {NumFailures} failures to parse in {Elapsed}",
                instanceId,
                blobName,
                itemsProcessed,
                numFailures,
                sw.Elapsed);
        }
    }
}
