using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public class NullEntityUpdater<T> : IComponentAffectingEntityUpdater<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        public Task<bool> Update(T groupEntity, DateTime cursor)
        {
            return Task.FromResult(false);
        }
    }
}
