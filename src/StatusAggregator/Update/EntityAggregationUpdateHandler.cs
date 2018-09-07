// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    /// <summary>
    /// Updates a <typeparamref name="TEntityAggregation"/> and its <typeparamref name="TAggregatedEntity"/>s.
    /// </summary>
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

        public async Task<bool> Update(TEntityAggregation aggregationEntity, DateTime cursor)
        {
            using (_logger.Scope("Updating aggregation '{AggregationRowKey}' given cursor {Cursor}.", aggregationEntity.RowKey, cursor))
            {
                if (!aggregationEntity.IsActive)
                {
                    _logger.LogInformation("Aggregation is inactive, cannot update.");
                    return true;
                }

                var aggregatedEntitiesQuery = _table.GetLinkedEntities<TAggregatedEntity, TEntityAggregation>(aggregationEntity);

                var aggregatedEntities = aggregatedEntitiesQuery.ToList();
                if (aggregatedEntities.Any())
                {
                    _logger.LogInformation("Aggregation has {ChildrenCount} children. Updating each child.", aggregatedEntities.Count);
                    foreach (var aggregatedEntity in aggregatedEntities)
                    {
                        await _aggregatedEntityUpdater.Update(aggregatedEntity, cursor);
                    }
                }
                else
                {
                    _logger.LogInformation("Aggregation has no children and must have been created manually, cannot update.");
                    return false;
                }

                var hasActiveAggregatedEntities = aggregatedEntitiesQuery
                    .Where(i => i.IsActive)
                    .ToList()
                    .Any();

                var hasRecentAggregatedEntities = aggregatedEntitiesQuery
                    .Where(i => i.EndTime > cursor - _groupEndDelay)
                    .ToList()
                    .Any();

                var shouldDeactivate = !hasActiveAggregatedEntities && !hasRecentAggregatedEntities;
                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating aggregation because its children are inactive and too old.");
                    var lastEndTime = aggregatedEntities
                        .Max(i => i.EndTime ?? DateTime.MinValue);
                    aggregationEntity.EndTime = lastEndTime;

                    await _table.InsertOrReplaceAsync(aggregationEntity);
                }
                else
                {
                    _logger.LogInformation("Aggregation has active or recent children so it will not be deactivated.");
                }

                return shouldDeactivate;
            }
        }
    }
}
