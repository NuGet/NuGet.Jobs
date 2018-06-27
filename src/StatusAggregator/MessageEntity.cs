using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator
{
    public class MessageEntity : TableEntity
    {
        public const string DefaultPartitionKey = "messages";

        public string EventRowKey { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; }
    }
}
