using System;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status;

namespace StatusAggregator.Table
{
    public class MessageEntity : TableEntity
    {
        public const string DefaultPartitionKey = "messages";

        public MessageEntity()
        {
        }

        public MessageEntity(EventEntity eventEntity, DateTime time, string contents)
            : base(DefaultPartitionKey, GetRowKey(eventEntity, time))
        {
            EventRowKey = eventEntity.RowKey;
            Time = time;
            Contents = contents;
        }

        public string EventRowKey { get; set; }
        public DateTime Time { get; set; }
        public string Contents { get; set; }

        public Message AsMessage()
        {
            return new Message(Time, Contents);
        }

        private static string GetRowKey(EventEntity eventEntity, DateTime time)
        {
            return $"{eventEntity.RowKey}_{time.ToString("o")}";
        }
    }
}
