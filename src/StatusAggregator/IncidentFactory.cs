using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentFactory : IEntityFactory<IncidentEntity, ParsedIncident>
    {
        private readonly ITableWrapper _table;

        public IncidentFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<IncidentEntity> Create(ParsedIncident input)
        {
            var entity = new IncidentEntity(
                input.Id,
                input.AffectedComponentPath,
                input.AffectedComponentStatus,
                input.CreationTime,
                input.MitigationTime);

            await _table.InsertAsync(entity);

            return entity;
        }
    }
}
