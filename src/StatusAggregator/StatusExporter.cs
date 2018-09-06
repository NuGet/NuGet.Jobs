// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class StatusExporter : IStatusExporter
    {
        private const string StatusBlobName = "status.json";
        private readonly TimeSpan _eventVisibilityPeriod;

        private readonly CloudBlobContainer _container;
        private readonly ITableWrapper _table;

        private readonly ILogger<StatusExporter> _logger;

        private static readonly JsonSerializerSettings _statusBlobJsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new StatusContractResolver(),
            Converters = new List<JsonConverter>() { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        public StatusExporter(
            CloudBlobContainer container, 
            ITableWrapper table,
            StatusAggregatorConfiguration configuration,
            ILogger<StatusExporter> logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventVisibilityPeriod = TimeSpan.FromDays(configuration?.EventVisibilityPeriodDays ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServiceStatus> Export()
        {
            using (_logger.Scope("Exporting service status."))
            {
                var rootComponent = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent();

                // Apply the active entities to the component tree.
                var activeIncidentGroups = _table
                    .GetActiveEntities<IncidentGroupEntity>()
                    .ToList();

                var activeEvents = _table
                    .GetActiveEntities<EventEntity>()
                    .ToList();
                
                var activeEntities = activeIncidentGroups
                    .Concat<IComponentAffectingEntity>(activeEvents)
                    // Only apply entities with a non-Up status.
                    .Where(e => e.AffectedComponentStatus != (int)ComponentStatus.Up)
                    // If multiple events are affecting a single region, the event with the highest severity should affect the component.
                    .GroupBy(e => e.AffectedComponentPath)
                    .Select(g => g.OrderByDescending(e => e.AffectedComponentStatus).First());

                foreach (var activeEntity in activeEntities)
                {
                    using (_logger.Scope("Applying active event affecting '{AffectedComponentPath}' of severity {AffectedComponentStatus} at {StartTime} to root component",
                        activeEntity.AffectedComponentPath, activeEntity.AffectedComponentStatus, activeEntity.StartTime))
                    {
                        var currentComponent = rootComponent.GetByPath(activeEntity.AffectedComponentPath);

                        if (currentComponent == null)
                        {
                            _logger.LogWarning("Couldn't find component corresponding to active event.");
                            continue;
                        }

                        currentComponent.Status = (ComponentStatus)activeEntity.AffectedComponentStatus;
                    }
                }

                var recentEvents = _table
                    .CreateQuery<EventEntity>()
                    .Where(e => (e.IsActive || (e.EndTime >= DateTime.UtcNow - _eventVisibilityPeriod)))
                    .ToList()
                    .SelectMany(e =>
                    {
                        var messages = _table.GetLinkedEntities<MessageEntity, EventEntity>(e)
                            .ToList()
                            // Don't show empty messages.
                            .Where(m => !string.IsNullOrEmpty(m.Contents));

                        var factory = new EventFactory(e);
                        foreach (var message in messages)
                        {
                            factory.AddMessage(message);
                        }

                        return factory.Events;
                    })
                    .Where(e => e.Messages != null && e.Messages.Any())
                    .ToList();

                ServiceStatus status;
                string statusJson;
                using (_logger.Scope("Serializing service status."))
                {
                    status = new ServiceStatus(rootComponent, recentEvents);
                    statusJson = JsonConvert.SerializeObject(status, _statusBlobJsonSerializerSettings);
                }

                using (_logger.Scope("Saving service status to blob storage."))
                {
                    var blob = _container.GetBlockBlobReference(StatusBlobName);
                    await blob.UploadTextAsync(statusJson);
                }

                return status;
            }
        }

        private class EventFactory
        {
            private readonly EventEntity _eventEntity;

            private readonly IList<Event> _events = new List<Event>();
            public IEnumerable<Event> Events
            {
                get
                {
                    CommitMessages();
                    return _events;
                }
            }

            private readonly IList<MessageEntity> _currentMessages = new List<MessageEntity>();

            public EventFactory(EventEntity eventEntity)
            {
                _eventEntity = eventEntity;
            }

            public void AddMessage(MessageEntity message)
            {
                switch ((MessageType)message.Type)
                {
                    case MessageType.Start:
                        CommitMessages();
                        _currentMessages.Add(message);

                        break;

                    case MessageType.End:
                        _currentMessages.Add(message);
                        CommitMessages();

                        break;

                    case MessageType.Manual:
                        _currentMessages.Add(message);

                        break;
                }
            }

            private void CommitMessages()
            {
                if (_currentMessages.Any())
                {
                    var startTime = _currentMessages.Min(m => m.Time);
                    var endTime = _currentMessages.Count > 1
                        ? _currentMessages.Max(m => m.Time)
                        : (DateTime?)null;

                    var newEvent = new Event(
                        _eventEntity.AffectedComponentPath,
                        startTime,
                        endTime,
                        _currentMessages
                            .Select(m => new Message(m.Time, m.Contents))
                            .ToList());

                    _events.Add(newEvent);

                    _currentMessages.Clear();
                }
            }
        }
    }
}
