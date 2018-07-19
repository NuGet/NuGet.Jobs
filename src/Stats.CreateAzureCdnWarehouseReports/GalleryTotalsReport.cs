﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class GalleryTotalsReport
        : ReportBase
    {
        private const string WarehouseStoredProcedureName = "[dbo].[SelectTotalDownloadCounts]";
        private const string GalleryQuery = @"SELECT
                    (SELECT COUNT(DISTINCT [PackageRegistrationKey]) FROM Packages p WITH (NOLOCK)
                            WHERE p.Listed = 1 AND p.Deleted = 0) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1 AND Deleted = 0) AS TotalPackages";
        internal const string ReportName = "stats-totals.json";

        private Func<Task<SqlConnection>> OpenGallerySqlConnectionAsync { get; }

        private Func<Task<SqlConnection>> OpenStatisticsSqlConnectionAsync { get; }

        public GalleryTotalsReport(
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            Func<Task<SqlConnection>> openStatisticsSqlConnectionAsync,
            ILogger<GalleryTotalsReport> logger,
            CloudStorageAccount cloudStorageAccount,
            string statisticsContainerName)
            : base(logger, new[] { new StorageContainerTarget(cloudStorageAccount, statisticsContainerName) })
        {
            OpenGallerySqlConnectionAsync = openGallerySqlConnectionAsync;
            OpenStatisticsSqlConnectionAsync = openStatisticsSqlConnectionAsync;
        }

        public async Task Run()
        {
            // gather package numbers from gallery database
            GalleryTotalsData totalsData;

            using (var connection = await OpenGallerySqlConnectionAsync())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                _logger.LogInformation("Gathering Gallery Totals from {GalleryDataSource}/{GalleryInitialCatalog}...",
                    connection.DataSource, connection.Database);

                totalsData = (await connection.QueryWithRetryAsync<GalleryTotalsData>(
                    GalleryQuery, commandType: CommandType.Text, transaction: transaction)).First();
            }

            _logger.LogInformation("Total packages: {TotalPackagesCount}", totalsData.TotalPackages);
            _logger.LogInformation("Unique packages: {UniquePackagesCount}", totalsData.UniquePackages);

            // gather download count data from statistics warehouse

            using (var connection = await OpenStatisticsSqlConnectionAsync())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                _logger.LogInformation("Gathering Gallery Totals from {StatisticsDataSource}/{StatisticsInitialCatalog}...",
                    connection.DataSource, connection.Database);

                totalsData.Downloads = (await connection.ExecuteScalarWithRetryAsync<long>(
                    WarehouseStoredProcedureName,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: TimeSpan.FromMinutes(5),
                    transaction: transaction));
            }

            _logger.LogInformation("Total downloads: {TotalDownloadsCount}", totalsData.Downloads);

            // write to blob
            totalsData.LastUpdateDateUtc = DateTime.UtcNow;

            var reportText = JsonConvert.SerializeObject(totalsData);

            foreach (var storageContainerTarget in Targets)
            {
                try
                {
                    var targetBlobContainer = await GetBlobContainer(storageContainerTarget);
                    var blob = targetBlobContainer.GetBlockBlobReference(ReportName);
                    _logger.LogInformation("Writing report to {ReportUri}", blob.Uri.AbsoluteUri);
                    blob.Properties.ContentType = "application/json";
                    await blob.UploadTextAsync(reportText);
                    _logger.LogInformation("Wrote report to {ReportUri}", blob.Uri.AbsoluteUri);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error writing report to storage account {StorageAccount}, container {ReportContainer}. {Exception}",
                        storageContainerTarget.StorageAccount.Credentials.AccountName,
                        storageContainerTarget.ContainerName,
                        ex);
                }
            }
        }
    }
}