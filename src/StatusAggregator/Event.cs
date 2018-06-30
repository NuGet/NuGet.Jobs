using StatusAggregator.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class Event
    {
        public Event()
        {
        }

        public Event(EventEntity eventEntity, IEnumerable<Message> messages)
        {
            AffectedComponentPath = eventEntity.AffectedComponentPath;
            AffectedComponentStatus = eventEntity.AffectedComponentStatus;
            StartTime = eventEntity.StartTime;
            EndTime = eventEntity.EndTime;
            Messages = messages;
        }

        public string AffectedComponentPath { get; set; }
        public ComponentStatus AffectedComponentStatus { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public IEnumerable<Message> Messages { get; set; }
    }
}
