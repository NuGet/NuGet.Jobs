using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdater
    {
        Task UpdateAllActive(DateTime cursor);
    }

    public interface IComponentAffectingEntityUpdater<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        Task<bool> Update(T groupEntity, DateTime cursor);
    }
}
