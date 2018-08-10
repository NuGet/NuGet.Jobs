using NuGet.Services.Status.Table;
using System;

namespace StatusAggregator.Manual
{
    public static class ManualStatusChangeUtility
    {
        public static bool UpdateEventIsActive(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
        {
            var shouldUpdateEvent = true;
            if (eventIsActive && eventEntity.EndTime != null)
            {
                throw new ArgumentException("An event cannot be reactivated!", nameof(eventIsActive));
            }
            else if (!eventIsActive && eventEntity.EndTime == null)
            {
                eventEntity.EndTime = timestamp;
            }
            else
            {
                shouldUpdateEvent = false;
            }

            return shouldUpdateEvent;
        }
    }
}
