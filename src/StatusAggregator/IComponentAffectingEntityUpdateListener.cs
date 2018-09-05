using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdateListener<T>
        where T : ComponentAffectingEntity
    {
        Task OnUpdate(T entity, DateTime cursor);
    }
}