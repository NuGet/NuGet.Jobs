using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EventFactory : IEntityFactory<EventEntity, IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;

        public EventFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<EventEntity> Create(IncidentGroupEntity input)
        {
            var entity = new EventEntity(input);
            await _table.InsertAsync(entity);
            return entity;
        }
    }
}
