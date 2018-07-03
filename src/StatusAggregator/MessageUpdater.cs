using System;
using System.Linq;
using System.Threading.Tasks;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class MessageUpdater : IMessageUpdater
    {
        private static TimeSpan EventStartDelay = TimeSpan.FromMinutes(15);

        private ITableWrapper _table;

        public MessageUpdater(ITableWrapper table)
        {
            _table = table;
        }

        public async Task CreateMessageForEventStart(EventEntity eventEntity, DateTime nextCreationTime)
        {
            // Enough time must have passed before we create a start message for an event.
            if (nextCreationTime > eventEntity.StartTime + EventStartDelay)
            {
                var messageEntity = new MessageEntity(eventEntity, eventEntity.StartTime, "WE ARE IMPACTED");
                await _table.InsertOrReplaceAsync(messageEntity);
            }
        }

        public async Task CreateMessageForEventEnd(EventEntity eventEntity)
        {
            if (!eventEntity.EndTime.HasValue)
            {
                throw new ArgumentException("Must pass in an event with an end time!", nameof(eventEntity));
            }

            // Only create a message if the event already has messages associated with it.
            if (_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
            {
                var messageEntity = new MessageEntity(eventEntity, eventEntity.EndTime.Value, "NO LONGER IMPACTED");
                await _table.InsertOrReplaceAsync(messageEntity);
            }
        }
    }
}
