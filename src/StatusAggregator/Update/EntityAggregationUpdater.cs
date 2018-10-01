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
    public class EntityAggregationUpdater<TAggregatedEntity, TEntityAggregation> 
        : IComponentAffectingEntityUpdater<TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity
    {
        public readonly TimeSpan _groupEndDelay;

        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TAggregatedEntity> _aggregatedEntityUpdater;

        private readonly ILogger<EntityAggregationUpdater<TAggregatedEntity, TEntityAggregation>> _logger;

        public EntityAggregationUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TAggregatedEntity> aggregatedEntityUpdater,
            StatusAggregatorConfiguration configuration,
            ILogger<EntityAggregationUpdater<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregatedEntityUpdater = aggregatedEntityUpdater 
                ?? throw new ArgumentNullException(nameof(aggregatedEntityUpdater));
            _groupEndDelay = TimeSpan.FromMinutes(configuration?.EventEndDelayMinutes 
                ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Update(TEntityAggregation aggregationEntity, DateTime cursor)
        {
            aggregationEntity = aggregationEntity ?? throw new ArgumentNullException(nameof(aggregationEntity));

            using (_logger.Scope("Updating aggregation {AggregationRowKey} given cursor {Cursor}.", aggregationEntity.RowKey, cursor))
            {
                if (!aggregationEntity.IsActive)
                {
                    _logger.LogInformation("Aggregation is inactive, cannot update.");
                    return;
                }
                
                var hasActiveOrRecentChildren = false;
                var children = _table
                    .GetChildEntities<TAggregatedEntity, TEntityAggregation>(aggregationEntity)
                    .ToList();

                if (children.Any())
                {
                    _logger.LogInformation("Aggregation has {ChildrenCount} children. Updating each child.", children.Count);
                    foreach (var child in children)
                    {
                        await _aggregatedEntityUpdater.Update(child, cursor);

                        hasActiveOrRecentChildren = 
                            hasActiveOrRecentChildren || 
                            child.IsActive || 
                            child.EndTime > cursor - _groupEndDelay;
                    }
                }
                else
                {
                    _logger.LogInformation("Aggregation has no children and must have been created manually, cannot update.");
                    return;
                }
                
                if (!hasActiveOrRecentChildren)
                {
                    _logger.LogInformation("Deactivating aggregation because its children are inactive and too old.");
                    var lastEndTime = children
                        .Max(i => i.EndTime ?? DateTime.MinValue);
                    aggregationEntity.EndTime = lastEndTime;

                    await _table.InsertOrReplaceAsync(aggregationEntity);
                }
                else
                {
                    _logger.LogInformation("Aggregation has active or recent children so it will not be deactivated.");
                }
            }
        }
    }
}
