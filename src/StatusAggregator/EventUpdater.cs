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
    public class EventUpdater : IEventUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IMessageUpdater _messageUpdater;

        private readonly ILogger<EventUpdater> _logger;

        public EventUpdater(
            ITableWrapper table,
            IMessageUpdater messageUpdater,
            ILogger<EventUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _messageUpdater = messageUpdater ?? throw new ArgumentNullException(nameof(messageUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> UpdateEvent(EventEntity eventEntity, DateTime cursor)
        {
            eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));

            using (_logger.Scope("Updating group '{GroupRowKey}' given cursor {Cursor}.", eventEntity.RowKey, cursor))
            {
                if (!eventEntity.IsActive)
                {
                    _logger.LogInformation("Group is inactive, cannot update.");
                    return false;
                }

                var groupsLinkedToEventQuery = _table.GetIncidentGroupsLinkedToEvent(eventEntity);

                var groupsLinkedToEvent = groupsLinkedToEventQuery.ToList();
                if (!groupsLinkedToEvent.Any())
                {
                    _logger.LogInformation("Event has no linked groups and must have been created manually, cannot update.");
                    return false;
                }

                var shouldDeactivate = !groupsLinkedToEventQuery
                        .Where(i => i.IsActive)
                        .ToList()
                        .Any();

                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating event because its groups are inactive and too old.");
                    var endTime = groupsLinkedToEvent
                        .Max(i => i.EndTime ?? DateTime.MinValue);
                    eventEntity.EndTime = endTime;

                    await _messageUpdater.CreateMessageForEventStart(eventEntity, endTime);
                    await _messageUpdater.CreateMessageForEventEnd(eventEntity);

                    await _table.InsertOrReplaceAsync(eventEntity);
                }
                else
                {
                    _logger.LogInformation("Group has active or recent incidents so it will not be deactivated.");
                    await _messageUpdater.CreateMessageForEventStart(eventEntity, cursor);
                }

                return shouldDeactivate;
            }
        }
    }
}
