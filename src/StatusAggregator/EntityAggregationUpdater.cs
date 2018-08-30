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
    public class EntityAggregationUpdater<TEntityAggregation, TAggregatedEntity> 
        : IComponentAffectingEntityUpdateHandler, IComponentAffectingEntityUpdateHandler<TEntityAggregation>
        where TEntityAggregation : ITableEntity, IEntityAggregation, new()
        where TAggregatedEntity : ITableEntity, IAggregatedEntity, new()
    {
        public readonly TimeSpan _groupEndDelay;

        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdateHandler<TAggregatedEntity> _aggregatedEntityUpdater;

        private readonly ILogger<EntityAggregationUpdater<TEntityAggregation, TAggregatedEntity>> _logger;

        public EntityAggregationUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdateHandler<TAggregatedEntity> aggregatedEntityUpdater,
            StatusAggregatorConfiguration configuration,
            ILogger<EntityAggregationUpdater<TEntityAggregation, TAggregatedEntity>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregatedEntityUpdater = aggregatedEntityUpdater 
                ?? throw new ArgumentNullException(nameof(aggregatedEntityUpdater));
            _groupEndDelay = TimeSpan.FromMinutes(configuration?.EventEndDelayMinutes 
                ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAllActive(DateTime cursor)
        {
            using (_logger.Scope("Updating active groups."))
            {
                var activeGroups = _table.GetActiveEntities<TEntityAggregation>().ToList();
                _logger.LogInformation("Updating {ActiveGroupsCount} active groups.", activeGroups.Count());
                foreach (var activeGroup in activeGroups)
                {
                    await Update(activeGroup, cursor);
                }
            }
        }

        public async Task<bool> Update(TEntityAggregation groupEntity, DateTime cursor)
        {
            using (_logger.Scope("Updating group '{GroupRowKey}' given cursor {Cursor}.", groupEntity.RowKey, cursor))
            {
                if (!groupEntity.IsActive)
                {
                    _logger.LogInformation("Group is inactive, cannot update.");
                    return false;
                }

                var entitiesLinkedToGroupQuery = _table.GetLinkedEntities<TAggregatedEntity>(groupEntity);

                var entitiesLinkedToGroup = entitiesLinkedToGroupQuery.ToList();
                if (entitiesLinkedToGroup.Any())
                {
                    foreach (var linkedEntity in entitiesLinkedToGroup)
                    {
                        await _aggregatedEntityUpdater.Update(linkedEntity, cursor);
                    }
                }
                else
                {
                    _logger.LogInformation("Group has no linked children and must have been created manually, cannot update.");
                    return false;
                }

                var hasActiveLinkedEntities = entitiesLinkedToGroupQuery
                    .Where(i => i.IsActive)
                    .ToList()
                    .Any();

                var hasRecentLinkedEntities = entitiesLinkedToGroupQuery
                    .Where(i => i.EndTime > cursor - _groupEndDelay)
                    .ToList()
                    .Any();

                var shouldDeactivate = !hasActiveLinkedEntities && !hasRecentLinkedEntities;
                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating group because its children are inactive and too old.");
                    var lastEndTime = entitiesLinkedToGroup
                        .Max(i => i.EndTime ?? DateTime.MinValue);
                    groupEntity.EndTime = lastEndTime;

                    await _table.InsertOrReplaceAsync(groupEntity);
                }
                else
                {
                    _logger.LogInformation("Group has active or recent children so it will not be deactivated.");
                }

                return shouldDeactivate;
            }
        }
    }
}
