using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator
{
    public interface IEntityFactory<TEntity, TInput>
        where TEntity : ITableEntity, new()
    {
        Task<TEntity> Create(TInput input);
    }
}
