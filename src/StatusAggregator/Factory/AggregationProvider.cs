// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class AggregationProvider<TAggregatedEntity, TEntityAggregation>
        : IAggregationProvider<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IAffectedComponentPathProvider<TEntityAggregation> _aggregationPathProvider;
        private readonly IAggregationManager<TAggregatedEntity, TEntityAggregation> _manager;
        private readonly IComponentAffectingEntityFactory<TEntityAggregation> _aggregationFactory;

        private readonly ILogger<AggregationProvider<TAggregatedEntity, TEntityAggregation>> _logger;

        public AggregationProvider(
            ITableWrapper table,
            IAffectedComponentPathProvider<TEntityAggregation> aggregationPathProvider,
            IAggregationManager<TAggregatedEntity, TEntityAggregation> manager,
            IComponentAffectingEntityFactory<TEntityAggregation> aggregationFactory,
            ILogger<AggregationProvider<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationPathProvider = aggregationPathProvider ?? throw new ArgumentNullException(nameof(aggregationPathProvider));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _aggregationFactory = aggregationFactory ?? throw new ArgumentNullException(nameof(aggregationFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TEntityAggregation> Get(ParsedIncident input)
        {
            TEntityAggregation aggregationEntity = null;

            var possiblePath = _aggregationPathProvider.Get(input);
            // Find an aggregation to link to
            var possibleAggregations = _table
                .CreateQuery<TEntityAggregation>()
                .Where(e =>
                    // The aggregation must affect the same path
                    e.AffectedComponentPath == possiblePath &&
                    // The aggregation must begin before or at the same time
                    e.StartTime <= input.StartTime &&
                    // The aggregation must be active or the aggregation must end after this incident begins
                    (e.IsActive || (e.EndTime >= input.StartTime)))
                .ToList();

            _logger.LogInformation("Found {AggregationCount} possible aggregations to link entity to with path {AffectedComponentPath}.", possibleAggregations.Count(), possiblePath);
            foreach (var possibleAggregation in possibleAggregations)
            {
                if (await _manager.CanBeAggregatedBy(input, possibleAggregation))
                {
                    _logger.LogInformation("Linking entity to aggregation.");
                    aggregationEntity = possibleAggregation;
                    break;
                }
            }

            if (aggregationEntity == null)
            {
                _logger.LogInformation("Could not find existing aggregation to link to, creating new aggregation to link entity to.");
                aggregationEntity = await _aggregationFactory.Create(input);
                _logger.LogInformation("Created new aggregation {AggregationRowKey} to link entity to.", aggregationEntity.RowKey);
            }

            return aggregationEntity;
        }
    }
}
