// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Services.KeyVault;

namespace StatusAggregator
{
    public class Job : JobBase
    {
        private const string StatusBlobName = "status.json";

        private static TimeSpan EventStartDelay = TimeSpan.FromMinutes(15);
        private static TimeSpan EventEndDelay = TimeSpan.FromMinutes(10);

        private CloudBlobContainer _container;
        private CloudTable _table;
        
        private Uri _incidentApiBaseUri;
        private X509Certificate2 _incidentApiCertificate;
        private string _incidentApiRoutingId;
        private string _incidentApiEnvironment;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusStorageAccount);
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();
            var containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusContainerName);
            _container = blobClient.GetContainerReference(containerName);

            var tableClient = storageAccount.CreateCloudTableClient();
            var tableName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusTableName);
            _table = tableClient.GetTableReference(tableName);

            _incidentApiBaseUri = new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiBaseUri));
            _incidentApiRoutingId = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiRoutingId);
            _incidentApiEnvironment = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiEnvironment);
            var incidentApiCertificateThumbprint = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiCertificateThumbprint);
            var incidentApiCertificateStoreName = 
                JobConfigurationManager.TryGetEnumArgument(
                    jobArgsDictionary, 
                    JobArgumentNames.StatusIncidentApiCertificateStoreName, 
                    StoreName.My);
            var incidentApiCertificateStoreLocation = 
                JobConfigurationManager.TryGetEnumArgument(
                    jobArgsDictionary, 
                    JobArgumentNames.StatusIncidentApiCertificateStoreLocation, 
                    StoreLocation.LocalMachine);
            _incidentApiCertificate = CertificateUtility.FindCertificateByThumbprint(
                incidentApiCertificateStoreName, 
                incidentApiCertificateStoreLocation, 
                incidentApiCertificateThumbprint, 
                true);
        }

        public override async Task Run()
        {
            // await _container.CreateIfNotExistsAsync();
            await _table.CreateIfNotExistsAsync();

            await AggregateEvents();
        }

        private async Task AggregateEvents()
        {
            // Check the status of any active incidents.
            var activeIncidentEntities = _table
                .CreateQuery<IncidentEntity>()
                .AsQueryable()
                .Where(i => i.PartitionKey == IncidentEntity.DefaultPartitionKey && i.IsActive);

            foreach (var activeIncidentEntity in activeIncidentEntities)
            {
                var activeIncident = await GetIncident(activeIncidentEntity.IncidentApiId);
                activeIncidentEntity.MitigationTime = activeIncident.MitigationData?.Date;
                var incidentOperation = TableOperation.InsertOrReplace(activeIncidentEntity);
                await _table.ExecuteAsync(incidentOperation);
            }

            // Fetch any new incidents.
            var lastCursor = GetCursor();
            var nextCursor = DateTime.MinValue;
            Console.WriteLine($"Read cursor at {lastCursor.ToString("o")}");
            var incidentParsers = new IIncidentParser[] { new ValidationDurationIncidentParser("PROD"), new OutdatedSearchServiceInstanceIncidentParser("PROD") };
            var parsedIncidents = new List<ParsedIncident>();
            string query = lastCursor == DateTime.MinValue
                ? GetIncidentApiIncidentListAllIncidentsQuery()
                : GetIncidentApiIncidentListRecentIncidentsQuery(lastCursor);
            var nextLink = GetIncidentApiUri(GetIncidentApiIncidentList(query));
            do
            {
                var incidents = await GetIncidentApiResponse<IncidentList>(nextLink);
                foreach (var incident in incidents.Incidents)
                {
                    if (incident.CreateDate <= lastCursor)
                    {
                        continue;
                    }

                    if (incident.CreateDate > nextCursor)
                    {
                        nextCursor = incident.CreateDate;
                    }

                    foreach (var incidentParser in incidentParsers)
                    {
                        if (incidentParser.TryParseIncident(incident, out var parsedIncident))
                        {
                            Console.WriteLine($"Found {parsedIncident.Id} affecting {parsedIncident.AffectedComponentPath} with status {parsedIncident.AffectedComponentStatus} from {parsedIncident.CreationTime} to {parsedIncident.MitigationTime}");
                            parsedIncidents.Add(parsedIncident);
                        }
                    }
                }

                nextLink = incidents.NextLink;
            } while (nextLink != null);

            // Close any active events that no longer have any active incidents.
            var creationTimesPerPath = parsedIncidents
                .GroupBy(i => i.AffectedComponentPath)
                .ToDictionary(g => g.Key, g => g.Min(i => i.CreationTime));

            DateTime minCreationTime = creationTimesPerPath.Any() 
                ? creationTimesPerPath.Min(t => t.Value) 
                : new[] { nextCursor, DateTime.UtcNow }.Max();

            var eventsToCheckClosure = _table
                .CreateQuery<EventEntity>()
                .AsQueryable()
                .Where(e => e.PartitionKey == EventEntity.DefaultPartitionKey && e.IsActive);

            foreach (var eventToCheckClosure in eventsToCheckClosure)
            {
                var nextCreationTime = creationTimesPerPath.ContainsKey(eventToCheckClosure.AffectedComponentPath)
                    ? creationTimesPerPath[eventToCheckClosure.AffectedComponentPath]
                    : minCreationTime;

                await UpdateEventAndCheckForClosure(eventToCheckClosure, nextCreationTime);
            }

            // Aggregate the new incidents and create new events if necessary.
            if (parsedIncidents.Any())
            {
                foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.CreationTime))
                {
                    Console.WriteLine($"Attempting to save {parsedIncident.Id}");
                    var incidentEntity = new IncidentEntity(parsedIncident);

                    // Find an event to attach this incident to
                    var possibleEvents = _table
                        .CreateQuery<EventEntity>()
                        .AsQueryable()
                        .Where(e =>
                            e.PartitionKey == EventEntity.DefaultPartitionKey &&
                            // The incident and the event must affect the same component
                            e.AffectedComponentPath == parsedIncident.AffectedComponentPath &&
                            // The event must begin before or at the same time as the incident
                            e.StartTime <= parsedIncident.CreationTime &&
                            // The event must be active or the event must end after this incident begins
                            (e.IsActive || (e.EndTime >= parsedIncident.CreationTime)))
                        .ToList();

                    Console.WriteLine($"Found {possibleEvents.Count()} possible events to link {parsedIncident.Id} to");

                    if (possibleEvents.Any())
                    {
                        foreach (var possibleEvent in possibleEvents)
                        {
                            if (!GetIncidentsLinkedToEvent(possibleEvent).ToList().Any())
                            {
                                Console.WriteLine($"Cannot link {parsedIncident.Id} to {possibleEvent.RowKey} because it is not linked to any incidents");
                                continue;
                            }
                            
                            if (await UpdateEventAndCheckForClosure(possibleEvent, parsedIncident.CreationTime))
                            {
                                Console.WriteLine($"Cannot link {parsedIncident.Id} to {possibleEvent.RowKey} because its incidents are inactive and too old");
                                continue;
                            }

                            Console.WriteLine($"Linking {parsedIncident.Id} to {possibleEvent.RowKey}");
                            incidentEntity.EventRowKey = possibleEvents.First().RowKey;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(incidentEntity.EventRowKey))
                    {
                        var eventEntity = new EventEntity(incidentEntity);
                        Console.WriteLine($"Could not find existing event to attach {parsedIncident.Id} to, creating new event {eventEntity.RowKey}");
                        var eventOperation = TableOperation.InsertOrReplace(eventEntity);
                        await _table.ExecuteAsync(eventOperation);
                    }

                    var incidentOperation = TableOperation.InsertOrReplace(incidentEntity);
                    await _table.ExecuteAsync(incidentOperation);
                }
            }

            // Update the cursor to signify that we've fetched all incidents thus far.
            if (nextCursor > lastCursor)
            {
                var cursorEntity = new CursorEntity(nextCursor);
                var operation = TableOperation.InsertOrReplace(cursorEntity);
                await _table.ExecuteAsync(operation);
            }
        }

        private async Task<bool> UpdateEventAndCheckForClosure(EventEntity eventEntity, DateTime nextCreationTime)
        {
            if (!eventEntity.IsActive)
            {
                // Inactive events have already been closed.
                return false;
            }

            var incidentsLinkedToEventToClose = GetIncidentsLinkedToEvent(eventEntity);

            if (!incidentsLinkedToEventToClose.ToList().Any())
            {
                // If an event has no linked incidents it must have been created manually and should not be closed automatically.
                return false;
            }

            var shouldClose = !incidentsLinkedToEventToClose
                    .Where(i => i.IsActive || i.MitigationTime > nextCreationTime - EventEndDelay)
                    .ToList()
                    .Any();

            if (shouldClose)
            {
                Console.WriteLine($"Closing {eventEntity.RowKey} because its incidents are inactive and too old");
                var mitigationTime = incidentsLinkedToEventToClose
                    .ToList()
                    .Max(i => i.MitigationTime ?? DateTime.MinValue);

                await CreateMessageForEventStartIfTimeHasPassed(eventEntity, mitigationTime);

                // Create a message to alert customers that the event is resolved.
                // Only create a message if the event already has messages associated with it.
                var messagesForEvent = _table
                    .CreateQuery<MessageEntity>()
                    .AsQueryable()
                    .Where(m =>
                        m.PartitionKey == MessageEntity.DefaultPartitionKey &&
                        m.EventRowKey == eventEntity.RowKey)
                    .ToList();

                if (messagesForEvent.Any())
                {
                    var messageEntity = new MessageEntity(eventEntity, mitigationTime, "NO LONGER IMPACTED");
                    var messageOperation = TableOperation.InsertOrReplace(messageEntity);
                    await _table.ExecuteAsync(messageOperation);
                }

                // Update the event
                eventEntity.EndTime = mitigationTime;
                var eventOperation = TableOperation.InsertOrReplace(eventEntity);
                await _table.ExecuteAsync(eventOperation);
            }
            else
            {
                await CreateMessageForEventStartIfTimeHasPassed(eventEntity, nextCreationTime);
            }

            return shouldClose;
        }

        private async Task CreateMessageForEventStartIfTimeHasPassed(EventEntity eventEntity, DateTime currentTime)
        {
            if (currentTime > eventEntity.StartTime + EventStartDelay)
            {
                var messageEntity = new MessageEntity(eventEntity, eventEntity.StartTime, "WE ARE IMPACTED");
                var messageOperation = TableOperation.InsertOrReplace(messageEntity);
                await _table.ExecuteAsync(messageOperation);
            }
        }

        private IQueryable<IncidentEntity> GetIncidentsLinkedToEvent(EventEntity eventEntity)
        {
            return _table
                .CreateQuery<IncidentEntity>()
                .AsQueryable()
                .Where(i =>
                    i.PartitionKey == IncidentEntity.DefaultPartitionKey &&
                    i.IsLinkedToEvent &&
                    i.EventRowKey == eventEntity.RowKey);
        }

        private DateTime GetCursor()
        {
            var query = new TableQuery<CursorEntity>()
                .Where(TableQuery.GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey), 
                    QueryComparisons.Equal, 
                    CursorEntity.DefaultPartitionKey));

            var cursors = _table.ExecuteQuery(query).ToArray();
            return cursors.Any()
                ? cursors.Max(c => c.Value)
                : DateTime.MinValue;
        }

        private const string IncidentApiIncidentsEndpoint = "incidents";
        private static readonly string IncidentApiIndividualIncidentQueryFormatString = $"{IncidentApiIncidentsEndpoint}({{0}})";
        private static readonly string IncidentApiIncidentListQueryFormatString = $"{IncidentApiIncidentsEndpoint}?{{0}}";

        private static JsonSerializerSettings _incidentApiJsonSerializerSettings = new JsonSerializerSettings() { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
        
        private string GetIncidentApiIncidentList(string oDataQueryParameters)
        {
            return string.Format(IncidentApiIncidentListQueryFormatString, oDataQueryParameters);
        }

        private string GetIncidentApiGetIncidentQuery(string id)
        {
            return string.Format(IncidentApiIndividualIncidentQueryFormatString, id);
        }

        private string GetIncidentApiIncidentListAllIncidentsQuery()
        {
            return $"$filter=RoutingId eq '{_incidentApiRoutingId}'";
        }

        private string GetIncidentApiIncidentListRecentIncidentsQuery(DateTime cursor)
        {
            return $"$filter=RoutingId eq '{_incidentApiRoutingId}' and CreateDate gt datetime'{cursor.ToString("o")}'";
        }

        private Uri GetIncidentApiUri(string query)
        {
            return new Uri(_incidentApiBaseUri, query);
        }

        private Task<Incident> GetIncident(string id)
        {
            return GetIncidentApiResponse<Incident>(GetIncidentApiGetIncidentQuery(id));
        }

        private Task<T> GetIncidentApiResponse<T>(string query)
        {
            return GetIncidentApiResponse<T>(GetIncidentApiUri(query));
        }

        private async Task<T> GetIncidentApiResponse<T>(Uri uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.ClientCertificates.Add(_incidentApiCertificate);
            var response = await request.GetResponseAsync();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var content = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(content, _incidentApiJsonSerializerSettings);
            }
        }
    }
}
