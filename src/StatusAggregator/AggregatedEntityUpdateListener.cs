using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class AggregatedEntityUpdateListener<TAggregatedEntity, TEntityAggregation> : IComponentAffectingEntityUpdateListener<TAggregatedEntity>
        where TAggregatedEntity : ChildComponentAffectingEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TEntityAggregation> _aggregationUpdater;

        public AggregatedEntityUpdateListener(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TEntityAggregation> aggregationUpdater)
        {
            _table = table;
            _aggregationUpdater = aggregationUpdater;
        }

        public async Task OnUpdate(TAggregatedEntity aggregatedEntity, DateTime cursor)
        {
            var entityAggregation = await _table.RetrieveAsync<TEntityAggregation>(aggregatedEntity.ParentRowKey);
            if (entityAggregation != null)
            {
                await _aggregationUpdater.Update(entityAggregation, cursor);
            }
        }
    }
}
