using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    public class ActiveEventEntityUpdater : IActiveEventEntityUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<EventEntity> _updater;

        private readonly ILogger<ActiveEventEntityUpdater> _logger;

        public ActiveEventEntityUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<EventEntity> handler,
            ILogger<ActiveEventEntityUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _updater = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAll(DateTime cursor)
        {
            using (_logger.Scope("Updating active events."))
            {
                var activeEvents = _table.GetActiveEntities<EventEntity>().ToList();
                _logger.LogInformation("Updating {ActiveEventsCount} active events.", activeEvents.Count());
                foreach (var activeEvent in activeEvents)
                {
                    await _updater.Update(activeEvent, cursor);
                }
            }
        }
    }
}
