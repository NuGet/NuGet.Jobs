// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class RankingsReport
        : ReportBase
    {
        private const int RankingCount = 250;
        private const string RankingsQuery =
                  @"SELECT  TOP(250)
                            [Dimension_Package].[PackageId],
                            SUM
                            (
                                CASE
                                    WHEN LOWER([Dimension_Operation].[Operation]) = 'install'
                                    THEN [Fact_Download].[DownloadCount]
                                    ELSE (0.5 * [Fact_Download].[DownloadCount])
                                END
                            ) 'Downloads'
                    FROM    [Fact_Download]

                    INNER JOIN  [Dimension_Package]
                    ON          [Dimension_Package].[Id] = [Fact_Download].[Dimension_Package_Id]

                    INNER JOIN  [Dimension_Date]
                    ON          [Dimension_Date].[Id] = [Fact_Download].[Dimension_Date_Id]

                    INNER JOIN  [Dimension_Operation]
                    ON          [Dimension_Operation].[Id] = [Fact_Download].[Dimension_Operation_Id]

                    WHERE   [Dimension_Date].[Date] >= CONVERT(DATE, DATEADD(day, -42, GETUTCDATE()))
                        AND [Dimension_Date].[Date] < CONVERT(DATE, GETUTCDATE())
                        AND (
                                LOWER([Dimension_Operation].[Operation]) = 'install'
                                OR
                                LOWER([Dimension_Operation].[Operation]) = 'update'
                            )

                    GROUP BY    [Dimension_Package].[PackageId]
                    ORDER BY    Downloads DESC";
        internal const string ReportName = "rankings.v1.json";

        public RankingsReport(CloudStorageAccount cloudStorageAccount, string statisticsContainerName, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
            : base(new [] { new StorageContainerTarget(cloudStorageAccount, statisticsContainerName) }, statisticsDatabase, galleryDatabase)
        {
        }

        public async Task Run()
        {
            // gather download count data from statistics warehouse
            IReadOnlyCollection<RankingsData> rankingsData;
            Trace.TraceInformation("Gathering Rankings from {0}/{1}...", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);
            using (var connection = await StatisticsDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                rankingsData = (await connection.QueryWithRetryAsync<RankingsData>(
                    RankingsQuery, commandType: CommandType.Text, transaction: transaction)).ToList();
            }
            Trace.TraceInformation("Finished gathering Rankings from {0}/{1}.", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);

            // write to blob
            var reportText = JsonConvert.SerializeObject(rankingsData);

            Trace.TraceInformation(reportText);

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