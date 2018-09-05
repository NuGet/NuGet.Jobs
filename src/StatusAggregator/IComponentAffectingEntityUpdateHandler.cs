using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdateHandler<T>
        where T : ComponentAffectingEntity
    {
        Task<bool> Update(T groupEntity, DateTime cursor);
    }
}