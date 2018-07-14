// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using StatusAggregator.Incidents;
using StatusAggregator.Incidents.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class Job : JobBase
    {
        private CloudBlobContainer _container;
        private ITableWrapper _table;

        private IStatusUpdater _statusUpdater;
        private IStatusExporter _statusExporter;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusStorageAccount);
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var tableName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusTableName);
            _table = new TableWrapper(storageAccount, tableName);

            var cursor = new Cursor(_table);

            var messageUpdater = new MessageUpdater(_table);
            var eventUpdater = new EventUpdater(_table, messageUpdater);
            var incidentFactory = new IncidentFactory(_table, eventUpdater);

            var incidentApiBaseUri = new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiBaseUri));
            var incidentApiRoutingId = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiRoutingId);
            var incidentApiCertificate = GetCertificateFromJson(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiCertificate));
            var incidentCollector = new IncidentCollector(incidentApiBaseUri, incidentApiCertificate, incidentApiRoutingId);

            var environments = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusEnvironment).Split(';');
            var maximumSeverity = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.StatusMaximumSeverity) ?? int.MaxValue;
            var aggregateIncidentParser = new AggregateIncidentParser(GetIncidentParsers(environments, maximumSeverity));
            var incidentUpdater = new IncidentUpdater(_table, eventUpdater, incidentCollector, aggregateIncidentParser, incidentFactory);

            _statusUpdater = new StatusUpdater(cursor, incidentUpdater);

            var blobClient = storageAccount.CreateCloudBlobClient();
            var containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusContainerName);
            _container = blobClient.GetContainerReference(containerName);

            _statusExporter = new StatusExporter(_container, _table);
        }

        public override async Task Run()
        {
            await _table.CreateIfNotExistsAsync();
            await _container.CreateIfNotExistsAsync();

            await _statusUpdater.Update();
            await _statusExporter.Export();
        }

        private IEnumerable<IIncidentParser> GetIncidentParsers(IEnumerable<string> environments, int maximumSeverity)
        {
            return new IIncidentParser[]
            {
                new ValidationDurationIncidentParser(environments, maximumSeverity),
                new OutdatedRegionalSearchServiceInstanceIncidentParser(environments, maximumSeverity),
                new OutdatedSearchServiceInstanceIncidentParser(environments, maximumSeverity)
            };
        }

        private static X509Certificate2 GetCertificateFromJson(string certJson)
        {
            var certJObject = JObject.Parse(certJson);

            var certData = certJObject["Data"].Value<string>();
            var certPassword = certJObject["Password"].Value<string>();

            var certBytes = Convert.FromBase64String(certData);
            return new X509Certificate2(certBytes, certPassword);
        }
    }
}
