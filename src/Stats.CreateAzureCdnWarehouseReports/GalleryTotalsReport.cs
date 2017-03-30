﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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

        public GalleryTotalsReport(CloudStorageAccount cloudStorageAccount, string statisticsContainerName, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
            : base(new[] { new StorageContainerTarget(cloudStorageAccount, statisticsContainerName) }, statisticsDatabase, galleryDatabase)
        {
        }

        public async Task Run()
        {
            // gather package numbers from gallery database
            GalleryTotalsData totalsData;
            Trace.TraceInformation("Gathering Gallery Totals from {0}/{1}...", GalleryDatabase.DataSource, GalleryDatabase.InitialCatalog);
            using (var connection = await GalleryDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                totalsData = (await connection.QueryWithRetryAsync<GalleryTotalsData>(
                    GalleryQuery, commandType: CommandType.Text, transaction: transaction)).First();
            }
            Trace.TraceInformation("Total packages: {0}", totalsData.TotalPackages);
            Trace.TraceInformation("Unique packages: {0}", totalsData.UniquePackages);

            // gather download count data from statistics warehouse
            Trace.TraceInformation("Gathering Gallery Totals from {0}/{1}...", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);
            using (var connection = await StatisticsDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                totalsData.Downloads = (await connection.ExecuteScalarWithRetryAsync<long>(
                    WarehouseStoredProcedureName,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: TimeSpan.FromMinutes(5),
                    transaction: transaction));
            }
            Trace.TraceInformation("Total downloads: {0}", totalsData.Downloads);

            // write to blob
            totalsData.LastUpdateDateUtc = DateTime.UtcNow;

            var reportText = JsonConvert.SerializeObject(totalsData);

            foreach (var storageContainerTarget in Targets)
            {
                try
                {
                    var targetBlobContainer = await GetBlobContainer(storageContainerTarget);
                    var blob = targetBlobContainer.GetBlockBlobReference(ReportName);
                    Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);
                    blob.Properties.ContentType = "application/json";
                    await blob.UploadTextAsync(reportText);
                    Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error writing report to storage account {0}, container {1}. {2} {3}",
                        storageContainerTarget.StorageAccount.Credentials.AccountName,
                        storageContainerTarget.ContainerName, ex.Message, ex.StackTrace);
                }
            }
        }
    }
}