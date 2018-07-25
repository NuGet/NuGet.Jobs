﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using NuGet.Jobs.Extensions;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class Job
        : JobBase
    {
        private const int DefaultPerPackageReportDegreeOfParallelism = 8; // Generate 
        private const int DefaultSqlCommandTimeoutSeconds = 1800; // 30 minute SQL command timeout by default
        private const string _recentPopularityDetailByPackageReportBaseName = "recentpopularitydetail_";
        private CloudStorageAccount _cloudStorageAccount;
        private CloudStorageAccount _dataStorageAccount;
        private string _statisticsContainerName;
        private string _reportName;
        private string[] _dataContainerNames;
        private int _sqlCommandTimeoutSeconds = DefaultSqlCommandTimeoutSeconds;
        private int _perPackageReportDegreeOfParallelism = DefaultPerPackageReportDegreeOfParallelism;

        private SqlConnectionStringBuilder StatisticsDatabase { get; set; }

        private static readonly IDictionary<string, string> _storedProcedures = new Dictionary<string, string>
        {
            {ReportNames.NuGetClientVersion, "[dbo].[DownloadReportNuGetClientVersion]" },
            {ReportNames.Last6Weeks, "[dbo].[DownloadReportLast6Weeks]" },
            {ReportNames.RecentCommunityPopularity, "[dbo].[DownloadReportRecentCommunityPopularity]" },
            {ReportNames.RecentCommunityPopularityDetail, "[dbo].[DownloadReportRecentCommunityPopularityDetail]" },
            {ReportNames.RecentPopularity, "[dbo].[DownloadReportRecentPopularity]" },
            {ReportNames.RecentPopularityDetail, "[dbo].[DownloadReportRecentPopularityDetail]" },
        };

        private static readonly IDictionary<string, string> _storedProceduresPerPackageId = new Dictionary<string, string>
        {
            {ReportNames.RecentPopularityDetailByPackageId, "[dbo].[DownloadReportRecentPopularityDetailByPackage]" }
        };


        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            StatisticsDatabase = RegisterDatabase(serviceContainer, jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
            RegisterDatabase(serviceContainer, jobArgsDictionary, JobArgumentNames.SourceDatabase);

            _sqlCommandTimeoutSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.CommandTimeOut) ?? DefaultSqlCommandTimeoutSeconds;

            var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
            var dataStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
            _perPackageReportDegreeOfParallelism = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.PerPackageReportDegreeOfParallelism) ?? DefaultPerPackageReportDegreeOfParallelism;

            _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString, JobArgumentNames.AzureCdnCloudStorageAccount);
            _statisticsContainerName = ValidateAzureContainerName(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName), JobArgumentNames.AzureCdnCloudStorageContainerName);
            _dataStorageAccount = ValidateAzureCloudStorageAccount(dataStorageAccountConnectionString, JobArgumentNames.DataStorageAccount);
            _reportName = ValidateReportName(JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.WarehouseReportName));

            var containerNames = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataContainerName)
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var containerName in containerNames)
            {
                ValidateAzureContainerName(containerName, JobArgumentNames.DataContainerName);
            }

            _dataContainerNames = containerNames;
        }

        public Task<SqlConnection> OpenStatisticsSqlConnectionAsync()
        {
            return OpenSqlConnectionAsync(JobArgumentNames.StatisticsDatabase);
        }

        public Task<SqlConnection> OpenGallerySqlConnectionAsync()
        {
            return OpenSqlConnectionAsync(JobArgumentNames.SourceDatabase);
        }

        public override async Task Run()
        {
            var reportGenerationTime = DateTime.UtcNow;
            var destinationContainer = _cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(_statisticsContainerName);

            Logger.LogDebug("Generating reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}",
                StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog,
                _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

            var reportBuilderLogger = LoggerFactory.CreateLogger<ReportBuilder>();
            var reportCollectorLogger = LoggerFactory.CreateLogger<ReportDataCollector>();

            if (string.IsNullOrEmpty(_reportName))
            {
                // generate all reports
                var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                    {
                        { new ReportBuilder(reportBuilderLogger, ReportNames.NuGetClientVersion), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.NuGetClientVersion], _sqlCommandTimeoutSeconds) },
                        { new ReportBuilder(reportBuilderLogger, ReportNames.Last6Weeks), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.Last6Weeks], _sqlCommandTimeoutSeconds) },
                        { new ReportBuilder(reportBuilderLogger, ReportNames.RecentCommunityPopularity), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.RecentCommunityPopularity], _sqlCommandTimeoutSeconds) },
                        { new ReportBuilder(reportBuilderLogger, ReportNames.RecentCommunityPopularityDetail), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.RecentCommunityPopularityDetail], _sqlCommandTimeoutSeconds) },
                        { new ReportBuilder(reportBuilderLogger, ReportNames.RecentPopularity), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.RecentPopularity], _sqlCommandTimeoutSeconds) },
                        { new ReportBuilder(reportBuilderLogger, ReportNames.RecentPopularityDetail), new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[ReportNames.RecentPopularityDetail], _sqlCommandTimeoutSeconds) }
                    };

                foreach (var reportGenerator in reportGenerators)
                {
                    await ProcessReport(LoggerFactory, destinationContainer, reportGenerator.Key, reportGenerator.Value, reportGenerationTime);
                    ApplicationInsightsHelper.TrackReportProcessed(reportGenerator.Key.ReportName + " report");
                }

                await RebuildPackageReports(destinationContainer, reportGenerationTime);
                await CleanInactiveRecentPopularityDetailByPackageReports(destinationContainer, reportGenerationTime);
            }
            else
            {
                // generate only the specific report
                var reportBuilder = new ReportBuilder(reportBuilderLogger, _reportName);
                var reportDataCollector = new ReportDataCollector(OpenStatisticsSqlConnectionAsync, reportCollectorLogger, _storedProcedures[_reportName], _sqlCommandTimeoutSeconds);

                await ProcessReport(LoggerFactory, destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime);
            }

            Logger.LogInformation("Generated reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}",
                StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog,
                _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

            // totals reports
            var stopwatch = Stopwatch.StartNew();

            // build downloads.v1.json
            var targets = new List<StorageContainerTarget>();
            targets.Add(new StorageContainerTarget(_cloudStorageAccount, _statisticsContainerName));
            foreach (var dataContainerName in _dataContainerNames)
            {
                targets.Add(new StorageContainerTarget(_dataStorageAccount, dataContainerName));
            }
            var downloadCountReport = new DownloadCountReport(OpenStatisticsSqlConnectionAsync, LoggerFactory.CreateLogger<DownloadCountReport>(), targets);
            await downloadCountReport.Run();

            stopwatch.Stop();
            ApplicationInsightsHelper.TrackMetric(DownloadCountReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
            ApplicationInsightsHelper.TrackReportProcessed(DownloadCountReport.ReportName);
            stopwatch.Restart();

            // build stats-totals.json
            var galleryTotalsReport = new GalleryTotalsReport(OpenGallerySqlConnectionAsync, OpenStatisticsSqlConnectionAsync, LoggerFactory.CreateLogger<GalleryTotalsReport>(), _cloudStorageAccount, _statisticsContainerName);
            await galleryTotalsReport.Run();

            stopwatch.Stop();
            ApplicationInsightsHelper.TrackMetric(GalleryTotalsReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
            ApplicationInsightsHelper.TrackReportProcessed(GalleryTotalsReport.ReportName);


            // build tools.v1.json
            var toolsReport = new DownloadsPerToolVersionReport(OpenStatisticsSqlConnectionAsync, LoggerFactory.CreateLogger<DownloadsPerToolVersionReport>(), _cloudStorageAccount, _statisticsContainerName);
            await toolsReport.Run();

            stopwatch.Stop();
            ApplicationInsightsHelper.TrackMetric(DownloadsPerToolVersionReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
            ApplicationInsightsHelper.TrackReportProcessed(DownloadsPerToolVersionReport.ReportName);
            stopwatch.Restart();
        }

        private static async Task ProcessReport(ILoggerFactory loggerFactory, CloudBlobContainer destinationContainer, ReportBuilder reportBuilder, ReportDataCollector reportDataCollector, DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            var dataTable = await reportDataCollector.CollectAsync(reportGenerationTime, parameters);
            if (dataTable.Rows.Count == 0)
            {
                return;
            }

            var json = reportBuilder.CreateReport(dataTable);

            var reportWriter = new ReportWriter(loggerFactory.CreateLogger<ReportWriter>(), destinationContainer);
            await reportWriter.WriteReport(reportBuilder.ReportArtifactName, json);
        }

        private async Task RebuildPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            var dirtyPackageIds = await ReportDataCollector.GetDirtyPackageIds(OpenStatisticsSqlConnectionAsync, LoggerFactory.CreateLogger<ReportDataCollector>(), reportGenerationTime, _sqlCommandTimeoutSeconds);

            if (!dirtyPackageIds.Any())
                return;

            // first process the top 100 packages
            var top100 = dirtyPackageIds.Take(100);
            var reportDataCollector = new ReportDataCollector(OpenStatisticsSqlConnectionAsync, LoggerFactory.CreateLogger<ReportDataCollector>(), _storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId], _sqlCommandTimeoutSeconds);
            var top100Task = Parallel.ForEach(top100, new ParallelOptions { MaxDegreeOfParallelism = _perPackageReportDegreeOfParallelism }, dirtyPackageId =>
            {
                var packageId = dirtyPackageId.PackageId.ToLowerInvariant();
                var reportBuilder = new RecentPopularityDetailByPackageReportBuilder(LoggerFactory.CreateLogger<RecentPopularityDetailByPackageReportBuilder>(), ReportNames.RecentPopularityDetailByPackageId, "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName + packageId);

                ProcessReport(LoggerFactory, destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                ApplicationInsightsHelper.TrackReportProcessed(reportBuilder.ReportName + " report", packageId);
            });

            // once top 100 is processed, continue with the rest
            if (top100Task.IsCompleted)
            {
                var excludingTop100 = dirtyPackageIds.Skip(100);

                top100Task = Parallel.ForEach(excludingTop100, new ParallelOptions { MaxDegreeOfParallelism = _perPackageReportDegreeOfParallelism },
                    dirtyPackageId =>
                    {
                        // generate all reports
                        var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                        {
                            {
                                new RecentPopularityDetailByPackageReportBuilder(
                                    LoggerFactory.CreateLogger<RecentPopularityDetailByPackageReportBuilder>(),
                                    ReportNames.RecentPopularityDetailByPackageId,
                                    "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName +
                                    dirtyPackageId.PackageId.ToLowerInvariant()),
                                new ReportDataCollector(
                                    OpenStatisticsSqlConnectionAsync,
                                    LoggerFactory.CreateLogger<ReportDataCollector>(),
                                    _storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId],
                                    _sqlCommandTimeoutSeconds)
                            }
                        };

                        foreach (var reportGenerator in reportGenerators)
                        {
                            ProcessReport(LoggerFactory, destinationContainer, reportGenerator.Key, reportGenerator.Value,
                                reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                            ApplicationInsightsHelper.TrackReportProcessed(reportGenerator.Key.ReportName + " report",
                                dirtyPackageId.PackageId.ToLowerInvariant());
                        }
                    });

                if (top100Task.IsCompleted)
                {
                    var runToCursor = dirtyPackageIds.First().RunToCuror;
                    await ReportDataCollector.UpdateDirtyPackageIdCursor(OpenStatisticsSqlConnectionAsync, runToCursor, _sqlCommandTimeoutSeconds);
                }
            }
        }

        private async Task CleanInactiveRecentPopularityDetailByPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            Logger.LogDebug("Getting list of inactive packages.");
            var packageIds = await ReportDataCollector.ListInactivePackageIdReports(OpenStatisticsSqlConnectionAsync, reportGenerationTime, _sqlCommandTimeoutSeconds);
            Logger.LogInformation("Found {InactivePackageCount} inactive packages.", packageIds.Count);

            // Collect the list of reports
            var subContainer = "recentpopularity/";
            Logger.LogDebug("Collecting list of package detail reports");
            var reports = destinationContainer.ListBlobs(subContainer + _recentPopularityDetailByPackageReportBaseName)
                    .OfType<CloudBlockBlob>()
                    .Select(b => b.Name);

            var reportSet = new HashSet<string>(reports);
            Logger.LogInformation("Collected {PackageDetailReportCount} package detail reports", reportSet.Count);

            Parallel.ForEach(packageIds, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async id =>
             {
                 string reportName = _recentPopularityDetailByPackageReportBaseName + id;
                 string blobName = subContainer + reportName + ".json";
                 if (reportSet.Contains(blobName))
                 {
                     var blob = destinationContainer.GetBlockBlobReference(blobName);
                     Logger.LogDebug("{ReportName}: Deleting empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);

                     await blob.DeleteIfExistsAsync();

                     Logger.LogInformation("{ReportName}: Deleted empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);
                 }
             });
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount, string parameterName)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException($"Job parameter {parameterName} is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }

            throw new ArgumentException($"Job parameter {parameterName} is invalid.");
        }

        private static string ValidateAzureContainerName(string containerName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException($"Job parameter {parameterName} is not defined.");
            }

            return containerName;
        }

        private static string ValidateReportName(string reportName)
        {
            if (string.IsNullOrWhiteSpace(reportName))
            {
                return null;
            }

            if (!_storedProcedures.ContainsKey(reportName.ToLowerInvariant()))
            {
                throw new ArgumentException("Job parameter ReportName contains unknown report name.");
            }

            return reportName;
        }

        private static class ReportNames
        {
            public const string NuGetClientVersion = "nugetclientversion";
            public const string Last6Weeks = "last6weeks";
            public const string RecentCommunityPopularity = "recentcommunitypopularity";
            public const string RecentCommunityPopularityDetail = "recentcommunitypopularitydetail";
            public const string RecentPopularity = "recentpopularity";
            public const string RecentPopularityDetail = "recentpopularitydetail";
            public const string RecentPopularityDetailByPackageId = "recentpopularitydetailbypackageid";
        }
    }
}