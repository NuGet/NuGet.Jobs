using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusUpdater : IStatusUpdater
    {
        private readonly ICursor _cursor;
        private readonly IIncidentUpdater _incidentUpdater;
        private readonly IEventUpdater _eventUpdater;

        private readonly ILogger<StatusUpdater> _logger;

        public StatusUpdater(
            ICursor cursor,
            IIncidentUpdater incidentUpdater,
            IEventUpdater eventUpdater,
            ILogger<StatusUpdater> logger)
        {
            _cursor = cursor;
            _incidentUpdater = incidentUpdater;
            _eventUpdater = eventUpdater;
            _logger = logger;
        }

        public async Task Update()
        {
            using (_logger.Scope(
                "Beginning to update service status.",
                "Finished updating service status.",
                "Updating service status."))
            {
                var lastCursor = await _cursor.Get();

                await _incidentUpdater.RefreshExistingIncidents();
                var nextCursor = await _incidentUpdater.FetchNewIncidents(lastCursor);

                await _eventUpdater.UpdateAllActiveEvents(nextCursor ?? DateTime.UtcNow);

                if (nextCursor.HasValue)
                {
                    await _cursor.Set(nextCursor.Value);
                }
            }
        }
    }
}
