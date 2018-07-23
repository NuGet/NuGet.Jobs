using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EventUpdater : IEventUpdater
    {
        public static TimeSpan EventEndDelay = TimeSpan.FromMinutes(10);

        private readonly ITableWrapper _table;
        private readonly IMessageUpdater _messageUpdater;

        public EventUpdater(ITableWrapper table, IMessageUpdater messageUpdater)
        {
            _table = table;
            _messageUpdater = messageUpdater;
        }

        public async Task<bool> UpdateEvent(EventEntity eventEntity, DateTime nextCreationTime)
        {
            eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));

            if (!eventEntity.IsActive)
            {
                // Inactive events cannot be updated.
                return false;
            }

            var incidentsLinkedToEventToCloseQuery = _table.GetIncidentsLinkedToEvent(eventEntity);

            var incidentsLinkedToEventToClose = incidentsLinkedToEventToCloseQuery.ToList();
            if (!incidentsLinkedToEventToClose.Any())
            {
                // If an event has no linked incidents it must have been created manually and should not be closed automatically.
                return false;
            }

            var shouldClose = !incidentsLinkedToEventToCloseQuery
                    .Where(i => i.IsActive || i.MitigationTime > nextCreationTime - EventEndDelay)
                    .ToList()
                    .Any();

            if (shouldClose)
            {
                Console.WriteLine($"Closing {eventEntity.RowKey} because its incidents are inactive and too old");
                var mitigationTime = incidentsLinkedToEventToClose
                    .Max(i => i.MitigationTime ?? DateTime.MinValue);
                eventEntity.EndTime = mitigationTime;

                await _messageUpdater.CreateMessageForEventStart(eventEntity, mitigationTime);
                await _messageUpdater.CreateMessageForEventEnd(eventEntity);

                // Update the event
                await _table.InsertOrReplaceAsync(eventEntity);
            }
            else
            {
                await _messageUpdater.CreateMessageForEventStart(eventEntity, nextCreationTime);
            }

            return shouldClose;
        }
    }
}
