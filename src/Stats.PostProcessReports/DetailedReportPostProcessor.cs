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
            ServicePointManager.DefaultConnectionLimit = 100;
            _logger.LogInformation("Connection limit: {ConnectionLimit}", ServicePointManager.DefaultConnectionLimit);
            var blobClient = _storageAccount.CreateCloudBlobClient();
            var sourceContainer = blobClient.GetContainerReference(_configuration.SourceContainerName);
            var destinationContainer = blobClient.GetContainerReference(_configuration.DestinationContainerName);
            BlobContinuationToken blobContinuationToken = null;
            var sourceBlobs = new ConcurrentBag<IListBlobItem>();
            string prefix = _configuration.SourcePath + _configuration.DetailedReportDirectoryName + "/";
            do
            {
                var segment = await sourceContainer.ListBlobsSegmentedAsync(prefix, blobContinuationToken);
                blobContinuationToken = segment.ContinuationToken;
                foreach (var blob in segment.Results.Where(b => b.Uri.AbsolutePath.EndsWith(".json")))
                {
                    sourceBlobs.Add(blob);
                }   
            } while (blobContinuationToken != null);

            foreach (var b in sourceBlobs)
            {
                _logger.LogInformation("Blob to consider: {BlobUri}", b.Uri.AbsoluteUri);
            }

            var individualReports = new ConcurrentBag<LineProcessingContext>();
            var endOfIngestion = new ManualResetEventSlim(false);

            var producerTasks = Enumerable
                .Range(1, 1)
                .Select(_ => ProcessBlobsAsync(sourceBlobs, individualReports))
                .ToList();

            async Task WaitForIngestion()
            {
                await Task.WhenAll(producerTasks);
                endOfIngestion.Set();
            }

            var consumerTasks = Enumerable
                .Range(1, 20)
                .Select(instanceId => WriteReports(instanceId, individualReports, destinationContainer, endOfIngestion))
                .ToList();

            await WaitForIngestion();
            await Task.WhenAll(consumerTasks);
            _logger.LogInformation("Done processing");
        }

        private class PackageIdContainer
        {
            public string PackageId { get; set; }
        };

        private async Task ProcessBlobsAsync(
            ConcurrentBag<IListBlobItem> sourceBlobs,
            ConcurrentBag<LineProcessingContext> individualReports)
        {
            while (sourceBlobs.TryTake(out var blob))
            {
                var blobPath = blob.Uri.AbsolutePath.Substring(blob.Uri.AbsolutePath.IndexOf('/', 1) + 1); // dropping container from path
                var sourceBlob = blob.Container.GetBlockBlobReference(blobPath);
                _logger.LogInformation("Processing {BlobUrl}", sourceBlob.Uri.AbsoluteUri);
                var sw = Stopwatch.StartNew();
                var numLines = 0;
                var blobStats = new BlobStatistics();
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
                            BlobUrl = sourceBlob.Uri.AbsoluteUri,
                            LineNumber = numLines,
                            Data = line,
                            BlobStatistics = blobStats,
                        });
                    }
                }
                sw.Stop();
                blobStats.TotalLineCount = numLines;
                _logger.LogInformation("Read {NumLines} lines from {BlobUrl} in {Elapsed}",
                    numLines,
                    sourceBlob.Uri.AbsoluteUri,
                    sw.Elapsed);

                while (individualReports.Count > 200)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        private async Task WriteReports(
            int instanceId,
            ConcurrentBag<LineProcessingContext> individualReports,
            CloudBlobContainer destinationContainer,
            ManualResetEventSlim ingestionFinished)
        {
            int numFailures = 0;
            int itemsProcessed = 0;
            _logger.LogInformation("Starting {InstanceId} instance of report writer", instanceId);
            var sw = Stopwatch.StartNew();
            while (!ingestionFinished.IsSet || !individualReports.IsEmpty)
            {
                if (!individualReports.TryTake(out LineProcessingContext details))
                {
                    // it seems we are consuming lines faster than they are produced
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }

                PackageIdContainer data;
                try
                {
                    data = JsonConvert.DeserializeObject<PackageIdContainer>(details.Data);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to parse line {LineNumber} in {BlobUrl}: {Line}", details.LineNumber, details.BlobUrl, details.Data);
                    details.BlobStatistics.IncrementLinesFailed();
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
                details.BlobStatistics.IncrementFileCreated();
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
            _logger.LogInformation("Instance {Instance} done processing. Processed {NumItems} items, got {NumFailures} failures to parse in {Elapsed}",
                instanceId,
                itemsProcessed,
                numFailures,
                sw.Elapsed);
        }
    }
}
