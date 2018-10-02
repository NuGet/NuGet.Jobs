// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator.Factory
{
    public class AggregationManager<TAggregatedEntity, TEntityAggregation>
        : IAggregationManager<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TEntityAggregation> _aggregationUpdater;

        private readonly ILogger<AggregationManager<TAggregatedEntity, TEntityAggregation>> _logger;

        public AggregationManager(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TEntityAggregation> aggregationUpdater,
            ILogger<AggregationManager<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationUpdater = aggregationUpdater ?? throw new ArgumentNullException(nameof(aggregationUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> CanBeAggregatedBy(ParsedIncident input, TEntityAggregation entityAggregation)
        {
            using (_logger.Scope("Determining if entity can be linked to aggregation {AggregationRowKey}", entityAggregation.RowKey))
            {
                if (!_table.GetChildEntities<TAggregatedEntity, TEntityAggregation>(entityAggregation).ToList().Any())
                {
                    _logger.LogInformation("Cannot link entity to aggregation because it is not linked to any incidents.");
                    return false;
                }

                await _aggregationUpdater.Update(entityAggregation, input.StartTime);
                if (!entityAggregation.IsActive)
                {
                    _logger.LogInformation("Cannot link entity to aggregation because it has been deactivated.");
                    return false;
                }

                _logger.LogInformation("Entity can be linked to aggregation.");
                return true;
            }
        }
    }
}
