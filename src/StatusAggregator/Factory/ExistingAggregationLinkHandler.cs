// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator.Factory
{
    public class ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>
        : IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TEntityAggregation> _aggregationUpdater;

        private readonly ILogger<ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>> _logger;

        public ExistingAggregationLinkHandler(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TEntityAggregation> aggregationUpdater,
            ILogger<ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table;
            _aggregationUpdater = aggregationUpdater;
            _logger = logger;
        }

        public async Task<bool> CanLink(ParsedIncident input, TEntityAggregation entityAggregation)
        {
            if (!_table.GetLinkedEntities<TAggregatedEntity, TEntityAggregation>(entityAggregation).ToList().Any())
            {
                _logger.LogInformation("Cannot link entity to aggregation because it is not linked to any incidents.");
                return false;
            }

            if (await _aggregationUpdater.Update(entityAggregation, input.StartTime))
            {
                _logger.LogInformation("Cannot link entity to aggregation because it has been deactivated.");
                return false;
            }

            return true;
        }
    }
}
