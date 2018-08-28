using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupFactory : IIncidentGroupFactory
    {
        private readonly ITableWrapper _table;

        private readonly IEventUpdater _eventUpdater;

        private readonly ILogger<IncidentGroupFactory> _logger;

        public IncidentGroupFactory(
            ITableWrapper table,
            IEventUpdater eventUpdater,
            ILogger<IncidentGroupFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventUpdater = eventUpdater ?? throw new ArgumentNullException(nameof(eventUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IncidentGroupEntity> CreateIncidentGroupAsync(IncidentEntity incidentEntity)
        {
            var incidentGroupEntity = new IncidentGroupEntity(incidentEntity);

            using (_logger.Scope("Creating group '{GroupRowKey}'.", incidentGroupEntity.RowKey))
            {
                // Find an event to attach this group to
                var possibleEvents = _table
                    .CreateQuery<EventEntity>()
                    .Where(e =>
                        e.PartitionKey == EventEntity.DefaultPartitionKey &&
                        // The group and the event must affect the same component
                        e.AffectedComponentPath.StartsWith(incidentGroupEntity.AffectedComponentPath) &&
                        // The event must begin before or at the same time as the group
                        e.StartTime <= incidentGroupEntity.StartTime &&
                        // The event must be active or the event must end after this group begins
                        (e.IsActive || (e.EndTime >= incidentGroupEntity.StartTime)))
                    .ToList();

                _logger.LogInformation("Found {EventCount} possible events to link group to.", possibleEvents.Count());
                EventEntity eventToLinkTo = null;
                foreach (var possibleEventToLinkTo in possibleEvents)
                {
                    if (!_table.GetIncidentGroupsLinkedToEvent(possibleEventToLinkTo).ToList().Any())
                    {
                        _logger.LogInformation("Cannot link group to event '{EventRowKey}' because it is not linked to any groups.", possibleEventToLinkTo.RowKey);
                        continue;
                    }

                    if (await _eventUpdater.UpdateEvent(possibleEventToLinkTo, incidentGroupEntity.StartTime))
                    {
                        _logger.LogInformation("Cannot link group to event '{EventRowKey}' because it has been deactivated.", possibleEventToLinkTo.RowKey);
                        continue;
                    }

                    _logger.LogInformation("Linking group to event '{EventRowKey}'.", possibleEventToLinkTo.RowKey);
                    eventToLinkTo = possibleEventToLinkTo;
                    break;
                }

                if (eventToLinkTo == null)
                {
                    eventToLinkTo = new EventEntity(incidentGroupEntity);
                    _logger.LogInformation("Could not find existing event to link to, creating new event '{EventRowKey}' to link group to.", eventToLinkTo.RowKey);
                    await _table.InsertOrReplaceAsync(eventToLinkTo);
                }

                incidentGroupEntity.EventRowKey = eventToLinkTo.RowKey;
                await _table.InsertOrReplaceAsync(incidentGroupEntity);

                return incidentGroupEntity;
            }
        }
    }
}
