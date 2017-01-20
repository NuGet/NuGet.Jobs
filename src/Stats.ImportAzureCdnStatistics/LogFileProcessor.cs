﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogFileProcessor
    {
        private const ushort _gzipLeadBytes = 0x8b1f;

        private readonly CloudBlobContainer _targetContainer;
        private readonly CloudBlobContainer _deadLetterContainer;
        private readonly ILogger _logger;
        private readonly Warehouse _warehouse;

        public LogFileProcessor(CloudBlobContainer targetContainer,
            CloudBlobContainer deadLetterContainer,
            ILoggerFactory loggerFactory,
            Warehouse warehouse)
        {
            if (targetContainer == null)
            {
                throw new ArgumentNullException(nameof(targetContainer));
            }

            if (deadLetterContainer == null)
            {
                throw new ArgumentNullException(nameof(deadLetterContainer));
            }

            if (warehouse == null)
            {
                throw new ArgumentNullException(nameof(warehouse));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _targetContainer = targetContainer;
            _deadLetterContainer = deadLetterContainer;
            _logger = loggerFactory.CreateLogger<Job>();

            _warehouse = warehouse;
        }

        public async Task ProcessLogFileAsync(ILeasedLogFile logFile, PackageStatisticsParser packageStatisticsParser, bool aggregatesOnly = false)
        {
            if (logFile == null)
            {
                return;
            }

            try
            {
                var logFileName = logFile.Blob.Name;
                var cdnStatistics = await ParseLogEntries(logFile, packageStatisticsParser, logFileName);
                var hasPackageStatistics = cdnStatistics.PackageStatistics.Any();
                var hasToolStatistics = cdnStatistics.ToolStatistics.Any();

                // replicate data to the statistics database
                if (hasPackageStatistics)
                {
                    _logger.LogInformation("Creating facts for package download statistics in {LogFileName}", logFileName);

                    var downloadFacts = await _warehouse.CreateAsync(cdnStatistics.PackageStatistics, logFileName);

                    if (downloadFacts != null)
                    {
                        // store facts recorded in this logfile
                        if (!aggregatesOnly)
                        {
                            await _warehouse.InsertDownloadFactsAsync(downloadFacts, logFileName);
                        }

                        // create aggregates for the logfile
                        var logFileAggregates = new LogFileAggregates(logFileName);
                        foreach (var table in downloadFacts)
                        {
                            if (string.Equals(table.TableName, "dbo.Fact_Download", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // aggregate download counts by date
                                var downloadsByDate =
                                    table.AsEnumerable()
                                        .GroupBy(e => e.Field<int>("Dimension_Date_Id"))
                                        .Select(e => new KeyValuePair<int, int>(e.Key, e.Count()));

                                foreach (var keyValuePair in downloadsByDate)
                                {
                                    logFileAggregates.PackageDownloadsByDateDimensionId.Add(keyValuePair.Key, keyValuePair.Value);

                                    _logger.LogInformation(
                                        "{LogFile} contains {PackageDownloadCount} package downloads for date id {DimensionDateId}",
                                        logFileName, keyValuePair.Value, keyValuePair.Key);
                                }
                            }
                        }

                        // store aggregates for this logfile
                        _logger.LogInformation("Storing aggregate facts for package download statistics in {LogFileName}", logFileName);
                        await _warehouse.StoreLogFileAggregatesAsync(logFileAggregates);
                    }
                }

                if (hasToolStatistics)
                {
                    _logger.LogInformation("Creating facts for tool download statistics in {LogFileName}", logFileName);

                    var downloadFacts = await _warehouse.CreateAsync(cdnStatistics.ToolStatistics, logFileName);

                    if (downloadFacts != null)
                    {
                        // store facts recorded in this logfile
                        if (!aggregatesOnly)
                        {
                            await _warehouse.InsertDownloadFactsAsync(downloadFacts, logFileName);
                        }
                    }
                }

                if (!aggregatesOnly)
                {
                    await ArchiveBlobAsync(logFile);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvents.FailedToProcessLogFile, e, "Unable to process {LogFile}", logFile.Uri);

                if (!aggregatesOnly)
                {
                    // copy the blob to a dead-letter container
                    await EnsureCopiedToContainerAsync(logFile, _deadLetterContainer, e);
                }
            }

            if (!aggregatesOnly)
            {
                // delete the blob from the 'to-be-processed' container
                await DeleteSourceBlobAsync(logFile);
            }
        }

        private static async Task EnsureCopiedToContainerAsync(ILeasedLogFile logFile, CloudBlobContainer targetContainer, Exception e = null)
        {
            var archivedBlob = targetContainer.GetBlockBlobReference(logFile.Blob.Name);
            if (!await archivedBlob.ExistsAsync())
            {
                await archivedBlob.StartCopyAsync(logFile.Blob);

                archivedBlob = (CloudBlockBlob)await targetContainer.GetBlobReferenceFromServerAsync(logFile.Blob.Name);

                while (archivedBlob.CopyState.Status == CopyStatus.Pending)
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    archivedBlob = (CloudBlockBlob)await targetContainer.GetBlobReferenceFromServerAsync(logFile.Blob.Name);
                }

                await archivedBlob.FetchAttributesAsync();

                if (e != null)
                {
                    // add the job error to the blob's metadata
                    if (archivedBlob.Metadata.ContainsKey("JobError"))
                    {
                        archivedBlob.Metadata["JobError"] = e.ToString().Replace("\r\n", string.Empty);
                    }
                    else
                    {
                        archivedBlob.Metadata.Add("JobError", e.ToString().Replace("\r\n", string.Empty));
                    }
                    await archivedBlob.SetMetadataAsync();
                }
                else if (archivedBlob.Metadata.ContainsKey("JobError"))
                {
                    archivedBlob.Metadata.Remove("JobError");
                    await archivedBlob.SetMetadataAsync();
                }
            }
        }

        private async Task<CdnStatistics> ParseLogEntries(ILeasedLogFile logFile, PackageStatisticsParser packageStatisticsParser, string fileName)
        {
            var logStream = await OpenCompressedBlobAsync(logFile);
            var blobUri = logFile.Uri;
            var blobName = logFile.Blob.Name;

            var packageStatistics = new List<PackageStatistics>();
            var toolStatistics = new List<ToolStatistics>();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // parse the log into table entities
                _logger.LogInformation("Beginning to parse blob {FtpBlobUri}.", blobUri);

                using (var logStreamReader = new StreamReader(logStream))
                {
                    var lineNumber = 0;
                    do
                    {
                        var rawLogLine = logStreamReader.ReadLine();
                        if (rawLogLine != null)
                        {
                            lineNumber++;

                            var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                                lineNumber,
                                rawLogLine,
                                (e, line) => _logger.LogError(
                                    LogEvents.FailedToParseLogFileEntry,
                                    e,
                                    LogMessages.ParseLogEntryLineFailed,
                                    fileName,
                                    line));

                            if (logEntry != null)
                            {
                                var statistic = packageStatisticsParser.FromCdnLogEntry(logEntry);
                                if (statistic != null)
                                {
                                    packageStatistics.Add(statistic);
                                }
                                else
                                {
                                    // check if this is a dist.nuget.org download
                                    if (logEntry.RequestUrl.Contains("dist.nuget.org/"))
                                    {
                                        var toolInfo = ToolStatisticsParser.FromCdnLogEntry(logEntry);
                                        if (toolInfo != null)
                                        {
                                            toolStatistics.Add(toolInfo);
                                        }
                                    }
                                }
                            }
                        }
                    } while (!logStreamReader.EndOfStream);
                }

                stopwatch.Stop();

                _logger.LogInformation("Finished parsing blob {FtpBlobUri} ({RecordCount} records).", blobUri, packageStatistics.Count);
                ApplicationInsightsHelper.TrackMetric("Blob parsing duration (ms)", stopwatch.ElapsedMilliseconds, blobName);
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _logger.LogError(LogEvents.FailedToParseLogFile, exception, "Failed to parse blob {FtpBlobUri}.", blobUri);
                ApplicationInsightsHelper.TrackException(exception, blobName);

                throw;
            }
            finally
            {
                logStream.Dispose();
            }


            var cdnStatistics = new CdnStatistics(packageStatistics, toolStatistics);
            return cdnStatistics;
        }

        private static async Task<bool> IsGzipCompressed(Stream stream)
        {
            stream.Position = 0;

            try
            {
                var bytes = new byte[4];
                await stream.ReadAsync(bytes, 0, 4);

                return BitConverter.ToUInt16(bytes, 0) == _gzipLeadBytes;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private async Task<Stream> OpenCompressedBlobAsync(ILeasedLogFile logFile)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Beginning opening of compressed blob {FtpBlobUri}.", logFile.Uri);

                var memoryStream = new MemoryStream();

                // decompress into memory (these are rolling log files and relatively small)
                using (var blobStream = await logFile.Blob.OpenReadAsync(AccessCondition.GenerateLeaseCondition(logFile.LeaseId), null, null))
                {
                    await blobStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                }

                stopwatch.Stop();

                _logger.LogInformation("Finished opening of compressed blob {FtpBlobUri}.", logFile.Uri);

                ApplicationInsightsHelper.TrackMetric("Open compressed blob duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);

                // verify if the stream is gzipped or not
                if (await IsGzipCompressed(memoryStream))
                {
                    return new GZipInputStream(memoryStream);
                }
                else
                {
                    return memoryStream;
                }
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _logger.LogError(LogEvents.FailedToDecompressBlob, exception, "Failed to open compressed blob {FtpBlobUri}", logFile.Uri);
                ApplicationInsightsHelper.TrackException(exception, logFile.Blob.Name);

                throw;
            }
        }

        private async Task ArchiveBlobAsync(ILeasedLogFile logFile)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await EnsureCopiedToContainerAsync(logFile, _targetContainer);

                _logger.LogInformation("Finished archive upload for blob {FtpBlobUri}.", logFile.Uri);

                stopwatch.Stop();
                ApplicationInsightsHelper.TrackMetric("Blob archiving duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _logger.LogError(LogEvents.FailedBlobUpload, exception, "Failed archive upload for blob {FtpBlobUri}", logFile.Uri);
                ApplicationInsightsHelper.TrackException(exception, logFile.Blob.Name);
                throw;
            }
        }

        private async Task DeleteSourceBlobAsync(ILeasedLogFile logFile)
        {
            if (await logFile.Blob.ExistsAsync())
            {
                try
                {
                    _logger.LogInformation("Beginning to delete blob {FtpBlobUri}.", logFile.Uri);

                    var accessCondition = AccessCondition.GenerateLeaseCondition(logFile.LeaseId);
                    await logFile.Blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, accessCondition, null, null);

                    _logger.LogInformation("Finished to delete blob {FtpBlobUri}.", logFile.Uri);
                }
                catch (Exception exception)
                {
                    _logger.LogError(LogEvents.FailedBlobDelete, exception, "Finished to delete blob {FtpBlobUri}", logFile.Uri);
                    ApplicationInsightsHelper.TrackException(exception, logFile.Blob.Name);
                    throw;
                }
            }
        }
    }
}