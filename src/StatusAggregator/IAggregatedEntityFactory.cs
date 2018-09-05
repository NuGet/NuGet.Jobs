using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator
{
    public interface IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : ChildComponentAffectingEntity<TEntityAggregation>
    {
        Task<TAggregatedEntity> Create(ParsedIncident input, TEntityAggregation aggregation);
    }
}
