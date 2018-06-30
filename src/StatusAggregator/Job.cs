// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Jobs;
using NuGet.Services.KeyVault;
using StatusAggregator.Incidents;
using StatusAggregator.Incidents.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class Job : JobBase
    {
        private const string StatusBlobName = "status.json";

        private static TimeSpan EventStartDelay = TimeSpan.FromMinutes(15);
        private static TimeSpan EventEndDelay = TimeSpan.FromMinutes(10);
        private static TimeSpan EventVisibilityPeriod = TimeSpan.FromDays(7);

        private CloudBlobContainer _container;
        private ITableWrapper _table;

        private ICursor _cursor;

        private IIncidentCollector _incidentCollector;
        private IEnumerable<IIncidentParser> _incidentParsers;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusStorageAccount);
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();
            var containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusContainerName);
            _container = blobClient.GetContainerReference(containerName);

            var tableName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusTableName);
            _table = new TableWrapper(storageAccount, tableName);

            _cursor = new Cursor(_table);

            var incidentApiBaseUri = new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiBaseUri));
            var incidentApiRoutingId = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiRoutingId);
            var incidentApiEnvironment = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiEnvironment);
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
            var incidentApiCertificate = CertificateUtility.FindCertificateByThumbprint(
                incidentApiCertificateStoreName, 
                incidentApiCertificateStoreLocation, 
                incidentApiCertificateThumbprint, 
                true);
            _incidentCollector = new IncidentCollector(incidentApiBaseUri, incidentApiCertificate, incidentApiRoutingId);
            _incidentParsers = GetIncidentParsers();
        }

        public override async Task Run()
        {
            await AggregateData();
            await ExportData();
        }

        private async Task AggregateData()
        {
            await _table.CreateIfNotExistsAsync();

            // Check the status of any active incidents.
            var activeIncidentEntities = _table
                .CreateQuery<IncidentEntity>()
                .Where(i => i.PartitionKey == IncidentEntity.DefaultPartitionKey && i.IsActive);

            foreach (var activeIncidentEntity in activeIncidentEntities)
            {
                var activeIncident = await _incidentCollector.GetIncident(activeIncidentEntity.IncidentApiId);
                activeIncidentEntity.MitigationTime = activeIncident.MitigationData?.Date;
                await _table.InsertOrReplaceAsync(activeIncidentEntity);
            }

            // Fetch any new incidents.
            var lastCursor = _cursor.Get();
            Console.WriteLine($"Read cursor at {lastCursor.ToString("o")}");

            var incidents = await _incidentCollector.GetRecentIncidents(lastCursor);
            var nextCursor = incidents.Any() ? incidents.Max(i => i.CreateDate) : lastCursor;
            var parsedIncidents = new List<ParsedIncident>();
            foreach (var incident in incidents)
            {
                foreach (var incidentParser in _incidentParsers)
                {
                    if (incidentParser.TryParseIncident(incident, out var parsedIncident))
                    {
                        parsedIncidents.Add(parsedIncident);
                    }
                }
            }

            // Close any active events that no longer have any active incidents.
            var creationTimesPerPath = parsedIncidents
                .GroupBy(i => i.AffectedComponentPath)
                .ToDictionary(g => g.Key, g => g.Min(i => i.CreationTime));

            DateTime minCreationTime = creationTimesPerPath.Any() 
                ? creationTimesPerPath.Min(t => t.Value) 
                : new[] { nextCursor, DateTime.UtcNow }.Max();

            var eventsToCheckClosure = GetActiveEvents();
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
                        await _table.InsertOrReplaceAsync(eventEntity);
                    }

                    await _table.InsertOrReplaceAsync(incidentEntity);
                }
            }

            // Update the cursor to signify that we've fetched all incidents thus far.
            if (nextCursor > lastCursor)
            {
                await _cursor.Set(nextCursor);
            }
        }

        private IEnumerable<IIncidentParser> GetIncidentParsers()
        {
            return new IIncidentParser[]
            {
                new ValidationDurationIncidentParser("PROD"),
                new OutdatedRegionalSearchServiceInstanceIncidentParser("PROD"),
                new OutdatedSearchServiceInstanceIncidentParser("PROD")
            };
        }

        private static readonly JsonSerializerSettings _statusBlobJsonSerializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Converters = new List<JsonConverter>() { new StringEnumConverter() }
        };

        private async Task ExportData()
        {
            await _container.CreateIfNotExistsAsync();

            var rootComponent = SetupRootComponent();

            var activeEvents = GetActiveEvents();
            foreach (var activeEvent in activeEvents)
            {
                var componentPathParts = activeEvent.AffectedComponentPath.Split(Component.ComponentPathDivider);

                IComponent currentComponent = new TreeComponent("", "", new[] { rootComponent });
                foreach (var componentPathPart in componentPathParts)
                {
                    currentComponent = currentComponent.SubComponents.FirstOrDefault(c => c.Name == componentPathPart);

                    if (currentComponent == null)
                    {
                        break;
                    }
                }

                if (currentComponent == null)
                {
                    continue;
                }

                currentComponent.Status = activeEvent.AffectedComponentStatus;
            }

            var recentEvents = _table
                .CreateQuery<EventEntity>()
                .Where(e => 
                    e.PartitionKey == EventEntity.DefaultPartitionKey && 
                    (e.IsActive || (e.EndTime >= DateTime.Now - EventVisibilityPeriod)))
                .ToList()
                .Select(e =>
                {
                    var messages = GetMessagesLinkedToEvent(e)
                        .ToList()
                        .Select(m => new Message(m));
                    return new Event(e, messages);
                });

            var status = new Status(rootComponent, recentEvents);
            var statusJson = JsonConvert.SerializeObject(status, _statusBlobJsonSerializerSettings);

            var blob = _container.GetBlockBlobReference(StatusBlobName);
            await blob.UploadTextAsync(statusJson);
        }

        private IComponent SetupRootComponent()
        {
            return new TreeComponent(
                "NuGet", 
                "", 
                new IComponent[] 
                {
                    new PrimarySecondaryComponent(
                        "NuGet.org", 
                        "Browsing the Gallery website", 
                        new[] 
                        {
                            new TreeComponent("USNC", "Primary region"),
                            new TreeComponent("USSC", "Backup region")
                        }),
                    new TreeComponent(
                        "Restore", 
                        "Downloading and installing packages from NuGet", 
                        new[] 
                        {
                            new TreeComponent(
                                "V3", 
                                "Restore using the V3 API", 
                                new[] 
                                {
                                    new TreeComponent("Global", "V3 restore for users outside of China"),
                                    new TreeComponent("China", "V3 restore for users inside China")
                                }),
                            new TreeComponent("V2", "Restore using the V2 API")
                        }),
                    new TreeComponent(
                        "Search", 
                        "Searching for new and existing packages in Visual Studio or the Gallery website", 
                        new[] 
                        {
                            new PrimarySecondaryComponent(
                                "Global", 
                                "Search for packages outside Asia", 
                                new[] 
                                {
                                    new TreeComponent("USNC", "Primary region"),
                                    new TreeComponent("USSC", "Backup region")
                                }),
                            new PrimarySecondaryComponent(
                                "Asia", 
                                "Search for packages inside Asia", 
                                new[] 
                                {
                                    new TreeComponent("EA", "Primary region"),
                                    new TreeComponent("SEA", "Backup region")
                                })
                        }),
                    new TreeComponent("Package Publishing", "Uploading new packages to NuGet.org")
                });
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
                if (GetMessagesLinkedToEvent(eventEntity).ToList().Any())
                {
                    var messageEntity = new MessageEntity(eventEntity, mitigationTime, "NO LONGER IMPACTED");
                    await _table.InsertOrReplaceAsync(messageEntity);
                }

                // Update the event
                eventEntity.EndTime = mitigationTime;
                await _table.InsertOrReplaceAsync(eventEntity);
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
                await _table.InsertOrReplaceAsync(messageEntity);
            }
        }

        private IQueryable<EventEntity> GetActiveEvents()
        {
            return _table
                .CreateQuery<EventEntity>()
                .Where(e => e.PartitionKey == EventEntity.DefaultPartitionKey && e.IsActive);
        }

        private IQueryable<IncidentEntity> GetIncidentsLinkedToEvent(EventEntity eventEntity)
        {
            return _table
                .CreateQuery<IncidentEntity>()
                .Where(i =>
                    i.PartitionKey == IncidentEntity.DefaultPartitionKey &&
                    i.IsLinkedToEvent &&
                    i.EventRowKey == eventEntity.RowKey);
        }

        private IQueryable<MessageEntity> GetMessagesLinkedToEvent(EventEntity eventEntity)
        {
            return _table
                .CreateQuery<MessageEntity>()
                .Where(m =>
                    m.PartitionKey == MessageEntity.DefaultPartitionKey &&
                    m.EventRowKey == eventEntity.RowKey);
        }
    }
}
