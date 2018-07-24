using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;


namespace StatusAggregator.Table
{
    public interface ITableWrapper
    {
        Task CreateIfNotExistsAsync();

        Task<T> Retrieve<T>(string partitionKey, string rowKey) 
            where T : class, ITableEntity;

        Task InsertOrReplaceAsync(ITableEntity tableEntity);

        IQueryable<T> CreateQuery<T>() where T : ITableEntity, new();
    }
}
