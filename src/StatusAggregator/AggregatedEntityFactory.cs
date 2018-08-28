using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    class AggregatedEntityFactory<TAggregatedEntity, TEntityAggregation, TInput> 
        : IEntityFactory<TAggregatedEntity, TInput>
        where TAggregatedEntity : class, ITableEntity, IAggregatedEntity, new()
        where TEntityAggregation : class, ITableEntity, IEntityAggregation, new()
    {
        private readonly ITableWrapper _table;
        private readonly IEntityFactory<TAggregatedEntity, TInput> _entityFactory;
        private readonly IEntityFactory<TEntityAggregation, TAggregatedEntity> _aggregationFactory;
        private readonly IComponentAffectingEntityUpdater<TEntityAggregation> _aggregationUpdater;

        private readonly ILogger<AggregatedEntityFactory<TAggregatedEntity, TEntityAggregation, TInput>> _logger;

        public AggregatedEntityFactory(
            ITableWrapper table,
            IEntityFactory<TAggregatedEntity, TInput> entityFactory,
            IEntityFactory<TEntityAggregation, TAggregatedEntity> aggregationFactory,
            IComponentAffectingEntityUpdater<TEntityAggregation> aggregationUpdater,
            ILogger<AggregatedEntityFactory<TAggregatedEntity, TEntityAggregation, TInput>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
            _aggregationFactory = aggregationFactory ?? throw new ArgumentNullException(nameof(aggregationFactory));
            _aggregationUpdater = aggregationUpdater ?? throw new ArgumentNullException(nameof(aggregationUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TAggregatedEntity> Create(TInput input)
        {
            var aggregatedEntity = await _entityFactory.Create(input);

            using (_logger.Scope("Creating incident '{IncidentRowKey}'.", aggregatedEntity.RowKey))
            {
                // Find an aggregation to link to
                var possibleAggregations = _table
                    .CreateQuery<TEntityAggregation>()
                    .Where(e =>
                        // The aggregation must affect the same component
                        e.AffectedComponentPath == aggregatedEntity.AffectedComponentPath &&
                        // The aggregation must begin before or at the same time
                        e.StartTime <= aggregatedEntity.StartTime &&
                        // The aggregation must be active or the aggregation must end after this incident begins
                        (e.IsActive || (e.EndTime >= aggregatedEntity.StartTime)))
                    .ToList();

                _logger.LogInformation("Found {GroupCount} possible groups to link incident to.", possibleAggregations.Count());
                TEntityAggregation groupToLinkTo = null;
                foreach (var possibleGroupToLinkTo in possibleAggregations)
                {
                    if (!_table.GetLinkedEntities<TAggregatedEntity>(possibleGroupToLinkTo).ToList().Any())
                    {
                        _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it is not linked to any incidents.", possibleGroupToLinkTo.RowKey);
                        continue;
                    }

                    if (await _aggregationUpdater.Update(possibleGroupToLinkTo, aggregatedEntity.StartTime))
                    {
                        _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it has been deactivated.", possibleGroupToLinkTo.RowKey);
                        continue;
                    }

                    _logger.LogInformation("Linking incident to group '{GroupRowKey}'.", possibleGroupToLinkTo.RowKey);
                    groupToLinkTo = possibleGroupToLinkTo;
                    break;
                }

                if (groupToLinkTo == null)
                {
                    _logger.LogInformation("Could not find existing group to link to, creating new group to link incident to.");
                    groupToLinkTo = await _aggregationFactory.Create(aggregatedEntity);
                    _logger.LogInformation("Created new group '{GroupRowKey}' to link incident to.", groupToLinkTo.RowKey);
                }

                aggregatedEntity.ParentRowKey = groupToLinkTo.RowKey;
                await _table.InsertOrReplaceAsync(aggregatedEntity);

                return aggregatedEntity;
            }
        }
    }
}
