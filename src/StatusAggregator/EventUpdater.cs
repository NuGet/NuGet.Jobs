using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EventUpdater : IEventUpdater
    {
        public static TimeSpan EventEndDelay = TimeSpan.FromMinutes(10);

        private readonly ITableWrapper _table;
        private readonly IMessageUpdater _messageUpdater;

        private readonly ILogger<EventUpdater> _logger;

        public EventUpdater(
            ITableWrapper table, 
            IMessageUpdater messageUpdater, 
            ILogger<EventUpdater> logger)
        {
            _table = table;
            _messageUpdater = messageUpdater;
            _logger = logger;
        }

        public async Task UpdateActiveEvents(DateTime cursor)
        {
            using (_logger.Scope("Updating active events."))
            {
                var activeEvents = _table.GetActiveEvents().ToList();
                _logger.LogInformation("Updating {ActiveEventsCount} active events.", activeEvents.Count());
                foreach (var activeEvent in activeEvents)
                {
                    await UpdateEvent(activeEvent, cursor);
                }
            }
        }

        public async Task<bool> UpdateEvent(EventEntity eventEntity, DateTime cursor)
        {
            eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));

            using (_logger.Scope("Updating event '{EventRowKey}' given cursor {Cursor}.", eventEntity.RowKey, cursor))
            {
                if (!eventEntity.IsActive)
                {
                    _logger.LogInformation("Event is inactive, cannot update.");
                    return false;
                }

                var incidentsLinkedToEventQuery = _table.GetIncidentsLinkedToEvent(eventEntity);

                var incidentsLinkedToEvent = incidentsLinkedToEventQuery.ToList();
                if (!incidentsLinkedToEvent.Any())
                {
                    _logger.LogInformation("Event has no linked incidents and must have been created manually, cannot update.");
                    return false;
                }

                var shouldDeactivate = !incidentsLinkedToEventQuery
                        .Where(i => i.IsActive || i.MitigationTime > cursor - EventEndDelay)
                        .ToList()
                        .Any();

                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating event because its incidents are inactive and too old.");
                    var mitigationTime = incidentsLinkedToEvent
                        .Max(i => i.MitigationTime ?? DateTime.MinValue);
                    eventEntity.EndTime = mitigationTime;

                    await _messageUpdater.CreateMessageForEventStart(eventEntity, mitigationTime);
                    await _messageUpdater.CreateMessageForEventEnd(eventEntity);

                    // Update the event
                    await _table.InsertOrReplaceAsync(eventEntity);
                }
                else
                {
                    _logger.LogInformation("Event has active or recent incidents so it will not be deactivated.");
                    await _messageUpdater.CreateMessageForEventStart(eventEntity, cursor);
                }

                return shouldDeactivate;
            }
        }
    }
}
