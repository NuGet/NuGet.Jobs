using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdateHandler<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        Task<bool> Update(T groupEntity, DateTime cursor);
    }
}