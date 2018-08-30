using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public class NullEntityAggregationLinkHandler<TEntityAggregation, TAggregatedEntity> : IEntityAggregationLinkHandler<TEntityAggregation, TAggregatedEntity>
        where TEntityAggregation : class, ITableEntity, IEntityAggregation, new()
        where TAggregatedEntity : class, ITableEntity, IAggregatedEntity, new()
    {
        public Task OnLink(TEntityAggregation entityAggregation, TAggregatedEntity aggregatedEntity)
        {
            return Task.CompletedTask;
        }
    }
}
