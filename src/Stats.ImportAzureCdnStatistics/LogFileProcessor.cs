﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class LogFileProcessor
    {
        private readonly IStatisticsBlobContainerUtility _statisticsBlobContainerUtility;
        private readonly ILogger _logger;
        private readonly IStatisticsWarehouse _warehouse;

        public LogFileProcessor(
            IStatisticsBlobContainerUtility statisticsBlobContainerUtility,
            ILoggerFactory loggerFactory,
            IStatisticsWarehouse warehouse)
        {
            if (statisticsBlobContainerUtility == null)
            {
                throw new ArgumentNullException(nameof(statisticsBlobContainerUtility));
            }

            if (warehouse == null)
            {
                throw new ArgumentNullException(nameof(warehouse));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _warehouse = warehouse;
            _statisticsBlobContainerUtility = statisticsBlobContainerUtility;
            _logger = loggerFactory.CreateLogger<Job>();
        }

        public async Task ProcessLogFileAsync(ILeasedLogFile logFile, IPackageStatisticsParser packageStatisticsParser, bool aggregatesOnly = false)
        {
            if (logFile == null)
            {
                return;
            }

            try
            {
                var logFileName = logFile.BlobName;
                var cdnStatistics = await ParseLogEntries(logFile, packageStatisticsParser, logFileName);
                var hasPackageStatistics = cdnStatistics.PackageStatistics.Any();
                var hasToolStatistics = cdnStatistics.ToolStatistics.Any();

                // replicate data to the statistics database
                if (hasPackageStatistics)
                {
                    await ProcessPackageStatisticsInLogFile(cdnStatistics, logFileName, aggregatesOnly);
                }

                if (hasToolStatistics)
                {
                    await ProcessToolStatisticsInLogFile(cdnStatistics, logFileName, aggregatesOnly);
                }

                if (!aggregatesOnly)
                {
                    await _statisticsBlobContainerUtility.ArchiveBlobAsync(logFile);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvents.FailedToProcessLogFile, e, "Unable to process {LogFile}", logFile.Uri);

                if (!aggregatesOnly)
                {
                    // copy the blob to a dead-letter container
                    await _statisticsBlobContainerUtility.CopyToDeadLetterContainerAsync(logFile, e);
                }
            }

            if (!aggregatesOnly)
            {
                // delete the blob from the 'to-be-processed' container
                await _statisticsBlobContainerUtility.DeleteSourceBlobAsync(logFile);
            }
        }

        private async Task ProcessToolStatisticsInLogFile(CdnStatistics cdnStatistics, string logFileName, bool aggregatesOnly)
        {
            // check if we already successfully imported tool statistics for this file
            if (await _warehouse.HasImportedToolStatisticsAsync(logFileName))
            {
                _logger.LogWarning(
                    "Already imported tool download statistics for {LogFileName}: skipping.", logFileName);
            }
            else
            {
                _logger.LogInformation("Creating facts for tool download statistics in {LogFileName}", logFileName);
                var downloadFacts = await _warehouse.CreateAsync(cdnStatistics.ToolStatistics, logFileName);

                // store facts recorded in this logfile
                if (downloadFacts != null && !aggregatesOnly)
                {
                    await _warehouse.InsertDownloadFactsAsync(downloadFacts, logFileName);
                }
            }
        }

        private async Task ProcessPackageStatisticsInLogFile(CdnStatistics cdnStatistics, string logFileName, bool aggregatesOnly)
        {
            // check if we already successfully imported package statistics for this file
            var alreadyImportedPackageStatistics = await _warehouse.HasImportedPackageStatisticsAsync(logFileName);
            _logger.LogInformation("Creating facts for package download statistics in {LogFileName}", logFileName);

            var downloadFacts = await _warehouse.CreateAsync(cdnStatistics.PackageStatistics, logFileName);
            if (downloadFacts != null)
            {
                // store facts recorded in this logfile
                if (!alreadyImportedPackageStatistics && !aggregatesOnly)
                {
                    await _warehouse.InsertDownloadFactsAsync(downloadFacts, logFileName);
                }
                else
                {
                    _logger.LogWarning("Already imported package download statistics for {LogFileName}: skipping.", logFileName);
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
                _logger.LogInformation(
                    "Storing aggregate facts for package download statistics in {LogFileName}",
                    logFileName);

                await _warehouse.StoreLogFileAggregatesAsync(logFileAggregates);
            }
        }

        private async Task<CdnStatistics> ParseLogEntries(ILeasedLogFile logFile, IPackageStatisticsParser packageStatisticsParser, string fileName)
        {
            var logStream = await _statisticsBlobContainerUtility.OpenCompressedBlobAsync(logFile);
            var blobUri = logFile.Uri;
            var blobName = logFile.BlobName;

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

            return new CdnStatistics(packageStatistics, toolStatistics);
        }
    }
}