using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>
    {
        Task OnLink(TEntityAggregation entityAggregation, TAggregatedEntity aggregatedEntity);
    }
}
