using StatusAggregator.Table;
using System;

namespace StatusAggregator
{
    public class Message
    {
        public Message()
        {
        }

        public Message(MessageEntity messageEntity)
        {
            Time = messageEntity.Time;
            Contents = messageEntity.Contents;
        }

        public DateTime Time { get; set; }
        public string Contents { get; set; }
    }
}
