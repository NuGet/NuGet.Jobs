using System;
using System.Collections.Generic;

namespace StatusAggregator
{
    public class Status
    {
        public Status()
        {
        }

        public Status(IComponent rootComponent, IEnumerable<Event> events)
        {
            LastUpdated = DateTime.Now;
            RootComponent = rootComponent;
            Events = events;
        }

        public DateTime LastUpdated { get; set; }
        public IComponent RootComponent { get; set; }
        public IEnumerable<Event> Events { get; set; }
    }
}
