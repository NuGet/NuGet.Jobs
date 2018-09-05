using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentFactory : IAggregatedEntityFactory<IncidentEntity, IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;

        public IncidentFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<IncidentEntity> Create(ParsedIncident input, IncidentGroupEntity groupEntity)
        {
            var entity = new IncidentEntity(
                input.Id,
                groupEntity,
                input.AffectedComponentPath,
                (ComponentStatus)input.AffectedComponentStatus,
                input.StartTime,
                input.EndTime);

            await _table.InsertOrReplaceAsync(entity);

            return entity;
        }
    }
}
