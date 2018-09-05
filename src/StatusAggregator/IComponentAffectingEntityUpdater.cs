using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdater
    {
        Task UpdateAllActive(DateTime cursor);
    }

    public interface IComponentAffectingEntityUpdater<T>
        where T : ComponentAffectingEntity
    {
        Task<bool> Update(T entity, DateTime cursor);
    }
}
