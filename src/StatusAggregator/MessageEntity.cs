using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator
{
    public class MessageEntity : TableEntity
    {
        public const string DefaultPartitionKey = "messages";

        public MessageEntity()
        {
        }

        public MessageEntity(EventEntity eventEntity, DateTime time, string message)
            : base(DefaultPartitionKey, GetRowKey(eventEntity, time))
        {
            EventRowKey = eventEntity.RowKey;
            Time = time;
            Message = message;
        }

        public string EventRowKey { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; }

        private static string GetRowKey(EventEntity eventEntity, DateTime time)
        {
            return $"{eventEntity.RowKey}_{time.ToString("o")}";
        }
    }
}
