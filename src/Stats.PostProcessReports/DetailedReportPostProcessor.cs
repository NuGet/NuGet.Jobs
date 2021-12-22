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
        private readonly IStorage _sourceStorage;
        private readonly IStorage _workStorage;
        private readonly IStorage _destinationStorage;
        private readonly CloudStorageAccount _storageAccount;
        private readonly PostProcessReportsConfiguration _configuration;
        private readonly ILogger<DetailedReportPostProcessor> _logger;

        public DetailedReportPostProcessor(
            IStorage sourceStorage,
            IStorage workStorage,
            IStorage destinationStorage,
            CloudStorageAccount cloudStorageAccount,
            IOptionsSnapshot<PostProcessReportsConfiguration> configurationAccessor,
            ILogger<DetailedReportPostProcessor> logger)
        {
            _sourceStorage = sourceStorage ?? throw new ArgumentNullException(nameof(sourceStorage));
            _workStorage = workStorage ?? throw new ArgumentNullException(nameof(workStorage));
            _destinationStorage = destinationStorage ?? throw new ArgumentNullException(nameof(destinationStorage));
            _storageAccount = cloudStorageAccount ?? throw new ArgumentNullException(nameof(cloudStorageAccount));
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value ?? throw new ArgumentException($"{nameof(configurationAccessor.Value)} property must not be null", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ServicePointManager.DefaultConnectionLimit = _configuration.ReportWriteDegreeOfParallelism + 10;
            _logger.LogInformation("Connection limit: {ConnectionLimit}", ServicePointManager.DefaultConnectionLimit);
        }

        public async Task CopyReportsAsync()
        {
            var sourceBlobs = await EnumerateSourceBlobsAsync();

            foreach (var sourceBlob in sourceBlobs)
            {
                var blobName = GetBlobName(sourceBlob);
                var targetUrl = _workStorage.ResolveUri(blobName);
                _logger.LogInformation("{SourceBlobUri} ({BlobName})", sourceBlob.Uri.AbsoluteUri, blobName);
                _logger.LogInformation("{WorkBlobUrl}", targetUrl);
                await _sourceStorage.CopyAsync(sourceBlob.Uri, _workStorage, targetUrl, destinationProperties: null, CancellationToken.None);
            }

            //foreach (var sourceBlob in sourceBlobs.Take(1))
            //{
            //    var sourceBlobStats = new BlobStatistics();
            //    var individualReports = await ProcessSourceBlobAsync(sourceBlob, sourceBlobStats);
            //    var blobName = sourceBlob.Uri.AbsoluteUri.Split('/').Last();
            //    using (_logger.BeginScope("Processing {BlobName}", blobName))
            //    {
            //        if (individualReports.Any())
            //        {
            //            var consumerTasks = Enumerable
            //                .Range(1, _configuration.ReportWriteDegreeOfParallelism)
            //                .Select(instanceId => WriteReports(instanceId, individualReports, destinationContainer, sourceBlobStats, blobName))
            //                .ToList();

            //            await Task.WhenAll(consumerTasks);
            //        }
            //    }
            //}

            _logger.LogInformation("Done processing");
        }

        private async Task<List<StorageListItem>> EnumerateSourceBlobsAsync()
        {
            var blobs = await _sourceStorage.List(CancellationToken.None);

            return blobs.ToList();
        }

        private class PackageIdContainer
        {
            public string PackageId { get; set; }
        };

        private async Task<ConcurrentBag<LineProcessingContext>> ProcessSourceBlobAsync(
            StorageListItem blob,
            BlobStatistics blobStats)
        {
            var blobPath = GetPathInContainer(blob);
            _logger.LogInformation("Processing {BlobUrl}", blob.Uri.AbsoluteUri);
            var sw = Stopwatch.StartNew();
            var numLines = 0;
            var individualReports = new ConcurrentBag<LineProcessingContext>();
            var storageContent = await _sourceStorage.Load(blob.Uri, CancellationToken.None);
            using (var sourceStream = storageContent.GetContentStream())
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
                blob.Uri.AbsoluteUri,
                sw.Elapsed);
            return individualReports;
        }

        private static string GetBlobName(StorageListItem blob)
        {
            var path = blob.Uri.AbsoluteUri;
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash < 0)
            {
                throw new ArgumentException($"Blob URI path does not contain '/': {blob.Uri.AbsolutePath}", nameof(blob));
            }

            return path.Substring(lastSlash + 1);
        }

        private static string GetPathInContainer(StorageListItem blob)
        {
            string path = blob.Uri.AbsolutePath;
            if (path.Length < 4)
            {
                // 1. Starting slash
                // 2. At least one character for a container name
                // 3. Slash separating container from the rest of the path
                // 4. At least one character for the rest of the path
                throw new ArgumentException($"Blob URI path must be at least 4 characters", nameof(blob));
            }

            // absolute paths start with slash, so we'll start searching for the next one
            // starting from the second character
            int secondSlash = path.IndexOf('/', 1);

            if (secondSlash < 0)
            {
                throw new ArgumentException($"Blob URI path must have at last 2 levels", nameof(blob));
            }

            return path.Substring(secondSlash + 1);
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
                        details.Data);
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
