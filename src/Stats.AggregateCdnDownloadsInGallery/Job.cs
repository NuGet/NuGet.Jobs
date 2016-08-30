﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;
using NuGet.Services.KeyVault;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class Job
        : JobBase
    {
        private const string _tempTableName = "#AggregateCdnDownloadsInGallery";

        private const string _createTempTable = @"
            IF OBJECT_ID('tempdb.dbo.#AggregateCdnDownloadsInGallery', 'U') IS NOT NULL
                DROP TABLE #AggregateCdnDownloadsInGallery

            CREATE TABLE #AggregateCdnDownloadsInGallery
            (
                [PackageRegistrationKey]    INT             NOT NULL,
                [PackageVersion]            NVARCHAR(255)	NOT NULL,
                [DownloadCount]             INT             NOT NULL,
            )";

        private const string _updateFromTempTable = @"
            -- Update Packages table
            UPDATE P SET P.[DownloadCount] = Stats.[DownloadCount]
            FROM [dbo].[Packages] AS P
            INNER JOIN #AggregateCdnDownloadsInGallery AS Stats ON Stats.[PackageRegistrationKey] = P.[PackageRegistrationKey]
            WHERE P.[Version] = Stats.[PackageVersion]

            -- Update PackageRegistrations table
            UPDATE PR SET PR.[DownloadCount] = AggregateStats.[DownloadCount]
            FROM [dbo].[PackageRegistrations] AS PR
            INNER JOIN (
	            SELECT Stats.[PackageRegistrationKey] AS [PackageRegistrationKey], SUM(Stats.[DownloadCount]) AS [DownloadCount]
	            FROM #AggregateCdnDownloadsInGallery AS Stats
	            GROUP BY Stats.[PackageRegistrationKey]
            ) AS AggregateStats ON AggregateStats.[PackageRegistrationKey] = PR.[Key]

            -- No more need for temp table
            DROP TABLE #AggregateCdnDownloadsInGallery";

        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private SqlConnectionStringBuilder _statisticsDatabase;
        private SqlConnectionStringBuilder _destinationDatabase;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        public override async Task<bool> Init(IArgumentsDictionary jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = await jobArgsDictionary.GetOrDefault<string>(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                _loggerFactory = LoggingSetup.CreateLoggerFactory();
                _logger = _loggerFactory.CreateLogger<Job>();

                var statisticsDatabaseConnectionString = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.StatisticsDatabase);
                _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);

                var destinationDatabaseConnectionString = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.DestinationDatabase);
                _destinationDatabase = new SqlConnectionStringBuilder(destinationDatabaseConnectionString);
            }
            catch (Exception exception)
            {
                _logger?.LogCritical("Failed to initialize job! {Exception}", exception);

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                // Gather download counts data from statistics warehouse
                IReadOnlyCollection<DownloadCountData> downloadData;
                _logger.LogDebug("Gathering Download Counts from {0}/{1}...", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog);

                using (var statisticsDatabase = await _statisticsDatabase.ConnectTo())
                using (var statisticsDatabaseTransaction = statisticsDatabase.BeginTransaction(IsolationLevel.Snapshot)) {
                    downloadData = (
                        await statisticsDatabase.QueryWithRetryAsync<DownloadCountData>(
                            _storedProcedureName,
                            transaction: statisticsDatabaseTransaction,
                            commandType: CommandType.StoredProcedure,
                            commandTimeout: (int)TimeSpan.FromMinutes(15).TotalSeconds,
                            maxRetries: 3))
                        .ToList();
                }

                _logger.LogInformation("Gathered {RecordCount} rows of data.", downloadData.Count);

                if (downloadData.Any())
                {
                    // Group based on Package Id
                    var packageRegistrationGroups = downloadData.GroupBy(p => p.PackageId).ToList();

                    using (var destinationDatabase = await _destinationDatabase.ConnectTo())
                    {
                        // Fetch package registrations so we can match
                        _logger.LogDebug("Retrieving package registrations...");
                        var packageRegistrationLookup = (
                            await destinationDatabase.QueryWithRetryAsync<PackageRegistrationData>(
                                "SELECT [Key], LOWER([Id]) AS Id FROM [dbo].[PackageRegistrations]",
                                commandTimeout: (int)TimeSpan.FromMinutes(10).TotalSeconds,
                                maxRetries: 5))
                            .Where(item => !string.IsNullOrEmpty(item.Id))
                            .ToDictionary(item => item.Id, item => item.Key);
                        _logger.LogInformation("Retrieved package registrations.");

                        // Create a temporary table
                        _logger.LogDebug("Creating temporary table...");
                        await destinationDatabase.ExecuteAsync(_createTempTable);

                        // Load temporary table
                        var aggregateCdnDownloadsInGalleryTable = new DataTable();
                        var command = new SqlCommand("SELECT * FROM " + _tempTableName, destinationDatabase);
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;
                        var reader = await command.ExecuteReaderAsync();
                        aggregateCdnDownloadsInGalleryTable.Load(reader);
                        aggregateCdnDownloadsInGalleryTable.Rows.Clear();
                        _logger.LogInformation("Created temporary table.");

                        // Populate temporary table in memory
                        _logger.LogDebug("Populating temporary table in memory...");
                        foreach (var packageRegistrationGroup in packageRegistrationGroups)
                        {
                            // don't process empty package id's
                            if (string.IsNullOrEmpty(packageRegistrationGroup.First().PackageId))
                            {
                                continue;
                            }

                            var packageId = packageRegistrationGroup.First().PackageId.ToLowerInvariant();

                            // Get package registration key
                            if (!packageRegistrationLookup.ContainsKey(packageId))
                            {
                                continue;
                            }
                            var packageRegistrationKey = packageRegistrationLookup[packageId];

                            // Set download count on individual packages
                            foreach (var package in packageRegistrationGroup)
                            {
                                var row = aggregateCdnDownloadsInGalleryTable.NewRow();
                                row["PackageRegistrationKey"] = packageRegistrationKey;
                                row["PackageVersion"] = package.PackageVersion;
                                row["DownloadCount"] = package.TotalDownloadCount;
                                aggregateCdnDownloadsInGalleryTable.Rows.Add(row);
                            }
                        }
                        _logger.LogInformation("Populated temporary table in memory. ({RecordCount} rows).", aggregateCdnDownloadsInGalleryTable.Rows.Count);

                        // Transfer to SQL database
                        _logger.LogDebug("Populating temporary table in database...");
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(destinationDatabase))
                        {
                            bulkcopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                            bulkcopy.DestinationTableName = _tempTableName;
                            bulkcopy.WriteToServer(aggregateCdnDownloadsInGalleryTable);
                            bulkcopy.Close();
                        }
                        _logger.LogInformation("Populated temporary table in database.");

                        // Update counts in destination database
                        _logger.LogDebug("Updating destination database Download Counts... ({RecordCount} package registrations to process).", packageRegistrationGroups.Count());

                        await destinationDatabase.ExecuteAsync(_updateFromTempTable,
                            timeout: (int)TimeSpan.FromMinutes(30).TotalSeconds);

                        _logger.LogInformation("Updated destination database Download Counts.");
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogCritical("Job run failed {Exception}!", exception);

                return false;
            }

            return true;
        }
    }
}