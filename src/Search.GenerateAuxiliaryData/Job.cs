// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.KeyVault;

namespace Search.GenerateAuxiliaryData
{
    public class Job : JsonConfigurationJob
    {
        private const string DefaultContainerName = "ng-search-data";

        private const string ScriptCuratedFeed = "SqlScripts.CuratedFeed.sql";
        private const string OutputNameCuratedFeed = "curatedfeeds.json";
        private const string Col0CuratedFeed = "FeedName";
        private const string Col1CuratedFeed = "Id";

        private const string ScriptOwners = "SqlScripts.Owners.sql";
        private const string OutputNameOwners = "owners.json";
        private const string Col0Owners = "Id";
        private const string Col1Owners = "UserName";

        private const string ScriptRankingsTotal = "SqlScripts.Rankings.sql";
        private const string OutputNameRankings = "rankings.v1.json";

        private const string ScriptVerifiedPackages = "SqlScripts.VerifiedPackages.sql";
        private const string OutputNameVerifiedPackages = "verifiedPackages.json";

        private const string StatisticsReportName = "downloads.v1.json";

        private List<Exporter> _exportersToRun;
        private CloudBlobContainer _destContainer;
        private CloudBlobContainer _statisticsContainer;

        private InitializationConfiguration Configuration { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            Configuration = _serviceProvider.GetRequiredService<InitializationConfiguration>();

            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));

            var statisticsStorageAccount = CloudStorageAccount.Parse(Configuration.AzureCdnCloudStorageAccount);

            var statisticsReportsContainerName = Configuration.AzureCdnCloudStorageContainerName;

            var destination = CloudStorageAccount.Parse(Configuration.PrimaryDestination);

            var destinationContainerName = Configuration.DestinationContainerName ?? DefaultContainerName;

            _destContainer = destination.CreateCloudBlobClient().GetContainerReference(destinationContainerName);
            _statisticsContainer = statisticsStorageAccount.CreateCloudBlobClient().GetContainerReference(statisticsReportsContainerName);

            _exportersToRun = new List<Exporter> {
                new VerifiedPackagesExporter(
                    LoggerFactory.CreateLogger<VerifiedPackagesExporter>(),
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    _destContainer,
                    ScriptVerifiedPackages,
                    OutputNameVerifiedPackages),

                new NestedJArrayExporter(
                    LoggerFactory.CreateLogger<NestedJArrayExporter>(),
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    _destContainer,
                    ScriptCuratedFeed,
                    OutputNameCuratedFeed,
                    Col0CuratedFeed,
                    Col1CuratedFeed),

                new NestedJArrayExporter(
                    LoggerFactory.CreateLogger<NestedJArrayExporter>(),
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    _destContainer,
                    ScriptOwners,
                    OutputNameOwners,
                    Col0Owners,
                    Col1Owners),

                new RankingsExporter(
                    LoggerFactory.CreateLogger<RankingsExporter>(),
                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                    _destContainer,
                    ScriptRankingsTotal,
                    OutputNameRankings),

                new BlobStorageExporter(
                    LoggerFactory.CreateLogger<BlobStorageExporter>(),
                    _statisticsContainer,
                    StatisticsReportName,
                    _destContainer,
                    StatisticsReportName)
            };
        }

        public override async Task Run()
        {
            var failedExporters = new List<string>();

            foreach (Exporter exporter in _exportersToRun)
            {
                try
                {
                    await exporter.ExportAsync();
                }
                catch (Exception e)
                {
                    var exporterName = exporter.GetType().Name;
                    Logger.LogError("SQL exporter '{ExporterName}' failed: {Exception}", exporterName, e);
                    failedExporters.Add(exporterName);
                }
            }
            
            if (failedExporters.Any())
            {
                throw new ExporterException($"{failedExporters.Count()} tasks failed: {string.Join(", ", failedExporters)}");
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }
    }
}