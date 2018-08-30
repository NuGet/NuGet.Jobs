using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class ComponentAffectingEntityUpdater<T> : IComponentAffectingEntityUpdater, IComponentAffectingEntityUpdater<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdateHandler<T> _handler;
        private readonly IComponentAffectingEntityUpdateListener<T> _listener;

        private readonly ILogger<ComponentAffectingEntityUpdater<T>> _logger;

        public ComponentAffectingEntityUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdateHandler<T> handler,
            IComponentAffectingEntityUpdateListener<T> listener,
            ILogger<ComponentAffectingEntityUpdater<T>> logger)
        {
            _table = table;
            _handler = handler;
            _listener = listener;
            _logger = logger;
        }

        public async Task UpdateAllActive(DateTime cursor)
        {
            using (_logger.Scope("Updating active groups."))
            {
                var activeGroups = _table.GetActiveEntities<T>().ToList();
                _logger.LogInformation("Updating {ActiveGroupsCount} active groups.", activeGroups.Count());
                foreach (var activeGroup in activeGroups)
                {
                    await _handler.Update(activeGroup, cursor);
                }
            }
        }

        public async Task<bool> Update(T groupEntity, DateTime cursor)
        {
            var result = await _handler.Update(groupEntity, cursor);
            await _listener.OnUpdate(groupEntity, cursor);

            return result;
        }
    }
}
