using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Status;
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
            var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
            var topLevelComponentPathParts = pathParts.Take(2).ToArray();
            var path = ComponentUtility.GetPath(topLevelComponentPathParts);
            var entity = new EventEntity(input, path);
            await _table.InsertOrReplaceAsync(entity);
            return entity;
        }
    }
}
