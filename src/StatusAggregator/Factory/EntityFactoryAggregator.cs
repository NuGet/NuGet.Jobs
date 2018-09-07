// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator.Factory
{
    /// <summary>
    /// Implementation of <see cref="IEntityFactory{TEntity}"/> that creates an <see cref="IAggregatedEntity{T}"/>.
    /// </summary>
    public class EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation> 
        : IEntityFactory<TAggregatedEntity>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation> _aggregatedEntityFactory;
        private readonly IEntityFactory<TEntityAggregation> _aggregationFactory;
        private readonly IComponentAffectingEntityUpdater<TEntityAggregation> _aggregationUpdater;
        private readonly IEnumerable<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> _aggregationLinkListeners;

        private readonly ILogger<EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>> _logger;

        public EntityFactoryAggregator(
            ITableWrapper table,
            IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation> entityFactory,
            IEntityFactory<TEntityAggregation> aggregationFactory,
            IComponentAffectingEntityUpdater<TEntityAggregation> aggregationUpdater,
            IEnumerable<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> aggregationLinkListeners,
            ILogger<EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregatedEntityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
            _aggregationFactory = aggregationFactory ?? throw new ArgumentNullException(nameof(aggregationFactory));
            _aggregationUpdater = aggregationUpdater ?? throw new ArgumentNullException(nameof(aggregationUpdater));
            _aggregationLinkListeners = aggregationLinkListeners ?? throw new ArgumentNullException(nameof(aggregationLinkListeners));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TAggregatedEntity> Create(ParsedIncident input)
        {
            TEntityAggregation aggregationEntity = null;
            using (_logger.Scope("Creating entity of type {AggregatedType} aggregated by type {AggregationType}.", 
                typeof(TAggregatedEntity), typeof(TEntityAggregation)))
            {
                // Iterate through the ancestors of the component that this parsed incident affects
                var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
                for (var i = 1; i <= pathParts.Length; i++)
                {
                    var possiblePath =
                        string.Join(
                            Constants.ComponentPathDivider.ToString(),
                            pathParts.Take(i).ToArray());
                    
                    // Find an aggregation to link to with that path
                    var possibleAggregations = _table
                        .CreateQuery<TEntityAggregation>()
                        .Where(e =>
                            // The aggregation must affect the same component or a parent component
                            e.AffectedComponentPath == possiblePath &&
                            // The aggregation must begin before or at the same time
                            e.StartTime <= input.StartTime &&
                            // The aggregation must be active or the aggregation must end after this incident begins
                            (e.IsActive || (e.EndTime >= input.StartTime)))
                        .ToList();

                    _logger.LogInformation("Found {AggregationCount} possible aggregations to link entity to with path {AffectedComponentPath}.", possibleAggregations.Count(), possiblePath);
                    foreach (var possibleAggregation in possibleAggregations)
                    {
                        using (_logger.Scope("Determining if entity can be linked to aggregation {AggregationRowKey}", possibleAggregation.RowKey))
                        {
                            if (!_table.GetLinkedEntities<TAggregatedEntity, TEntityAggregation>(possibleAggregation).ToList().Any())
                            {
                                _logger.LogInformation("Cannot link entity to aggregation because it is not linked to any incidents.");
                                continue;
                            }

                            if (await _aggregationUpdater.Update(possibleAggregation, input.StartTime))
                            {
                                _logger.LogInformation("Cannot link entity to aggregation because it has been deactivated.");
                                continue;
                            }

                            _logger.LogInformation("Linking entity to aggregation.");
                            aggregationEntity = possibleAggregation;
                            break;
                        }
                    }

                    if (aggregationEntity != null)
                    {
                        break;
                    }
                }

                if (aggregationEntity == null)
                {
                    _logger.LogInformation("Could not find existing aggregation to link to, creating new aggregation to link entity to.");
                    aggregationEntity = await _aggregationFactory.Create(input);
                    _logger.LogInformation("Created new aggregation {AggregationRowKey} to link entity to.", aggregationEntity.RowKey);
                }

                var aggregatedEntity = await _aggregatedEntityFactory.Create(input, aggregationEntity);
                foreach (var listener in _aggregationLinkListeners)
                {
                    await listener.OnLink(aggregationEntity, aggregatedEntity);
                }

                return aggregatedEntity;
            }
        }
    }
}
