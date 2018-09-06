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

namespace StatusAggregator
{
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
            TEntityAggregation groupToLinkTo = null;
            using (_logger.Scope("Creating entity."))
            {
                var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
                for (var i = 1; i <= pathParts.Length; i++)
                {
                    var possiblePath =
                        string.Join(
                            Constants.ComponentPathDivider.ToString(),
                            pathParts.Take(i).ToArray());
                    
                    // Find an aggregation to link to
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

                    _logger.LogInformation("Found {GroupCount} possible groups to link incident to with path {AffectedComponentPath}.", possibleAggregations.Count(), possiblePath);
                    foreach (var possibleGroupToLinkTo in possibleAggregations)
                    {
                        if (!_table.GetLinkedEntities<TAggregatedEntity, TEntityAggregation>(possibleGroupToLinkTo).ToList().Any())
                        {
                            _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it is not linked to any incidents.", possibleGroupToLinkTo.RowKey);
                            continue;
                        }

                        if (await _aggregationUpdater.Update(possibleGroupToLinkTo, input.StartTime))
                        {
                            _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it has been deactivated.", possibleGroupToLinkTo.RowKey);
                            continue;
                        }

                        _logger.LogInformation("Linking incident to group '{GroupRowKey}'.", possibleGroupToLinkTo.RowKey);
                        groupToLinkTo = possibleGroupToLinkTo;
                        break;
                    }

                    if (groupToLinkTo != null)
                    {
                        break;
                    }
                }

                if (groupToLinkTo == null)
                {
                    _logger.LogInformation("Could not find existing group to link to, creating new group to link incident to.");
                    groupToLinkTo = await _aggregationFactory.Create(input);
                    _logger.LogInformation("Created new group '{GroupRowKey}' to link incident to.", groupToLinkTo.RowKey);
                }

                var aggregatedEntity = await _aggregatedEntityFactory.Create(input, groupToLinkTo);
                foreach (var listener in _aggregationLinkListeners)
                {
                    await listener.OnLink(groupToLinkTo, aggregatedEntity);
                }

                return aggregatedEntity;
            }
        }
    }
}
