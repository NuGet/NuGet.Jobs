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

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var statisticsDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
            _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);

            var destinationDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DestinationDatabase);
            _destinationDatabase = new SqlConnectionStringBuilder(destinationDatabaseConnectionString);
        }

        public override async Task Run()
        {
            // Gather download counts data from statistics warehouse
            IReadOnlyCollection<DownloadCountData> downloadData;
            Logger.LogDebug("Gathering Download Counts from {0}/{1}...", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog);

            using (var statisticsDatabase = await _statisticsDatabase.ConnectTo())
            using (var statisticsDatabaseTransaction = statisticsDatabase.BeginTransaction(IsolationLevel.Snapshot))
            {
                downloadData = (
                    await statisticsDatabase.QueryWithRetryAsync<DownloadCountData>(
                        _storedProcedureName,
                        transaction: statisticsDatabaseTransaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: TimeSpan.FromMinutes(15),
                        maxRetries: 3))
                    .ToList();
            }

            Logger.LogInformation("Gathered {RecordCount} rows of data.", downloadData.Count);

            if (downloadData.Any())
            {
                // Group based on Package Id
                var packageRegistrationGroups = downloadData.GroupBy(p => p.PackageId).ToList();

                using (var destinationDatabase = await _destinationDatabase.ConnectTo())
                {
                    // Fetch package registrations so we can match
                    var packageRegistrationLookup = await GetPackageRegistrations(destinationDatabase);

                    // Create a temporary table
                    Logger.LogDebug("Creating temporary table...");
                    await destinationDatabase.ExecuteAsync(_createTempTable);

                    // Load temporary table
                    var aggregateCdnDownloadsInGalleryTable = new DataTable();
                    var command = new SqlCommand("SELECT * FROM " + _tempTableName, destinationDatabase);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;
                    var reader = await command.ExecuteReaderAsync();
                    aggregateCdnDownloadsInGalleryTable.Load(reader);
                    aggregateCdnDownloadsInGalleryTable.Rows.Clear();
                    aggregateCdnDownloadsInGalleryTable.TableName = $"dbo.{_tempTableName}";
                    Logger.LogInformation("Created temporary table.");

                    // Populate temporary table in memory
                    Logger.LogDebug("Populating temporary table in memory...");
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
                    Logger.LogInformation("Populated temporary table in memory. ({RecordCount} rows).", aggregateCdnDownloadsInGalleryTable.Rows.Count);

                    // Transfer to SQL database
                    Logger.LogDebug("Populating temporary table in database...");
                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(destinationDatabase))
                    {
                        bulkcopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                        bulkcopy.DestinationTableName = _tempTableName;
                        bulkcopy.WriteToServer(aggregateCdnDownloadsInGalleryTable);
                        bulkcopy.Close();
                    }
                    Logger.LogInformation("Populated temporary table in database.");

                    // Update counts in destination database
                    Logger.LogDebug("Updating destination database Download Counts... ({RecordCount} package registrations to process).", packageRegistrationGroups.Count());

                    await destinationDatabase.ExecuteAsync(_updateFromTempTable,
                        commandTimeout: TimeSpan.FromMinutes(30));

                    Logger.LogInformation("Updated destination database Download Counts.");
                }
            }
        }

        private async Task<IDictionary<string, string>> GetPackageRegistrations(SqlConnection sqlConnection)
        {
            Logger.LogDebug("Retrieving package registrations...");

            var packageRegistrationDictionary = new Dictionary<string, string>();

            // Ensure results are sorted deterministically.
            var packageRegistrationData = (await sqlConnection.QueryWithRetryAsync<PackageRegistrationData>(
                    "SELECT [Key], LOWER([Id]) AS LowercasedId, [Id] AS OriginalId FROM [dbo].[PackageRegistrations] (NOLOCK) ORDER BY [Id] ASC",
                    commandTimeout: TimeSpan.FromMinutes(10),
                    maxRetries: 5)).ToList();

            // We are not using .ToDictionary() and instead explicitly looping through these items to be able to detect
            // and avoid potential duplicate keys caused by LOWER([Id]) conflicts that may occur.
            foreach (var item in packageRegistrationData)
            {
                if (string.IsNullOrEmpty(item.LowercasedId))
                {
                    continue;
                }

                if (!packageRegistrationDictionary.ContainsKey(item.LowercasedId))
                {
                    packageRegistrationDictionary.Add(item.LowercasedId, item.Key);
                }
                else
                {
                    var conflictingPackageRegistration = packageRegistrationDictionary[item.LowercasedId];
                    var conflictingPackageOriginalId = packageRegistrationData.Single(p => p.Key == conflictingPackageRegistration);

                    // Lowercased package ID's should be unique, however, there's the case of the Turkish i...
                    Logger.LogWarning($"Package registration conflict detected: skipping package registration with key {item.Key} and ID {item.LowercasedId}." +
                                       $"Package {item.OriginalId} conflicts with package {conflictingPackageOriginalId}");
                }
            }

            Logger.LogInformation("Retrieved package registrations.");

            return packageRegistrationDictionary;
        }
    }
}