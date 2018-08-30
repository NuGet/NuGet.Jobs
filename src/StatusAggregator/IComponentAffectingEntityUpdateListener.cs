using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdateListener<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        Task OnUpdate(T entity, DateTime cursor);
    }
}