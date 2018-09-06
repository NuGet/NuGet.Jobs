using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EntityAggregationUpdateHandler<TEntityAggregation, TAggregatedEntity> 
        : IComponentAffectingEntityUpdateHandler<TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
    {
        public readonly TimeSpan _groupEndDelay;

        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TAggregatedEntity> _aggregatedEntityUpdater;

        private readonly ILogger<EntityAggregationUpdateHandler<TEntityAggregation, TAggregatedEntity>> _logger;

        public EntityAggregationUpdateHandler(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TAggregatedEntity> aggregatedEntityUpdater,
            StatusAggregatorConfiguration configuration,
            ILogger<EntityAggregationUpdateHandler<TEntityAggregation, TAggregatedEntity>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregatedEntityUpdater = aggregatedEntityUpdater 
                ?? throw new ArgumentNullException(nameof(aggregatedEntityUpdater));
            _groupEndDelay = TimeSpan.FromMinutes(configuration?.EventEndDelayMinutes 
                ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                var entitiesLinkedToGroupQuery = _table.GetLinkedEntities<TAggregatedEntity, TEntityAggregation>(groupEntity);

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
