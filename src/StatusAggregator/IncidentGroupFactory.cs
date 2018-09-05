using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupFactory : IAggregatedEntityFactory<IncidentGroupEntity, EventEntity>
    {
        private readonly ITableWrapper _table;

        public IncidentGroupFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<IncidentGroupEntity> Create(ParsedIncident input, EventEntity eventEntity)
        {
            var entity = new IncidentGroupEntity(
                eventEntity,
                input.AffectedComponentPath,
                (ComponentStatus)input.AffectedComponentStatus,
                input.StartTime);

            await _table.InsertOrReplaceAsync(entity);

            return entity;
        }
    }
}
