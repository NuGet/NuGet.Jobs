using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupFactory : IEntityFactory<IncidentGroupEntity, IncidentEntity>
    {
        private readonly ITableWrapper _table;

        public IncidentGroupFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<IncidentGroupEntity> Create(IncidentEntity input)
        {
            var entity = new IncidentGroupEntity(input);
            await _table.InsertOrReplaceAsync(entity);
            return entity;
        }
    }
}
