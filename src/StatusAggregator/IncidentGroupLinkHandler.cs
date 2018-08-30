using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupLinkHandler : IEntityAggregationLinkHandler<IncidentGroupEntity, IncidentEntity>
    {
        private readonly ITableWrapper _table;

        public IncidentGroupLinkHandler(ITableWrapper table)
        {
            _table = table;
        }

        public async Task OnLink(IncidentGroupEntity entityAggregation, IncidentEntity aggregatedEntity)
        {
            if (aggregatedEntity.AffectedComponentStatus > entityAggregation.AffectedComponentStatus)
            {
                entityAggregation.AffectedComponentStatus = aggregatedEntity.AffectedComponentStatus;
                await _table.ReplaceAsync(entityAggregation);
            }
        }
    }
}
