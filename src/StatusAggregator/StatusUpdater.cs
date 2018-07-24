using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusUpdater : IStatusUpdater
    {
        private readonly IIncidentUpdater _incidentUpdater;

        private ICursor _cursor;

        public StatusUpdater(
            ICursor cursor,
            IIncidentUpdater incidentUpdater)
        {
            _cursor = cursor;
            _incidentUpdater = incidentUpdater;
        }

        public async Task Update()
        {
            var lastCursor = await _cursor.Get();

            await _incidentUpdater.RefreshExistingIncidents();
            var nextCursor = await _incidentUpdater.FetchNewIncidents(lastCursor);
            await _incidentUpdater.UpdateActiveEvents(nextCursor ?? DateTime.UtcNow);
            
            if (nextCursor.HasValue)
            {
                await _cursor.Set(nextCursor.Value);
            }
        }
    }
}
