// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

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
        private readonly IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation> _aggregatedEntityFactory;
        private readonly IEntityFactory<TEntityAggregation> _aggregationFactory;
        private readonly IExistingAggregationProvider<TAggregatedEntity, TEntityAggregation> _existingAggregationProvider;
        private readonly IEnumerable<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> _aggregationLinkListeners;

        private readonly ILogger<EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>> _logger;

        public EntityFactoryAggregator(
            IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation> aggregatedEntityFactory,
            IEntityFactory<TEntityAggregation> aggregationFactory,
            IExistingAggregationProvider<TAggregatedEntity, TEntityAggregation> existingAggregationProvider,
            IEnumerable<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> aggregationLinkListeners,
            ILogger<EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _aggregatedEntityFactory = aggregatedEntityFactory ?? throw new ArgumentNullException(nameof(aggregatedEntityFactory));
            _aggregationFactory = aggregationFactory ?? throw new ArgumentNullException(nameof(aggregationFactory));
            _existingAggregationProvider = existingAggregationProvider ?? throw new ArgumentNullException(nameof(existingAggregationProvider));
            _aggregationLinkListeners = aggregationLinkListeners ?? throw new ArgumentNullException(nameof(aggregationLinkListeners));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TAggregatedEntity> Create(ParsedIncident input)
        {
            using (_logger.Scope("Creating entity of type {AggregatedType} aggregated by type {AggregationType}.", 
                typeof(TAggregatedEntity), typeof(TEntityAggregation)))
            {
                var aggregationEntity = await _existingAggregationProvider.GetExistingAggregation(input);

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
