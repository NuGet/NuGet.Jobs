using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator.Table
{
    public class TableWrapper : ITableWrapper
    {
        public TableWrapper(CloudStorageAccount storageAccount, string tableName)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(tableName);
        }

        private readonly CloudTable _table;

        public Task CreateIfNotExistsAsync()
        {
            return _table.CreateIfNotExistsAsync();
        }

        public Task InsertOrReplaceAsync(ITableEntity tableEntity)
        {
            var operation = TableOperation.InsertOrReplace(tableEntity);
            return _table.ExecuteAsync(operation);
        }

        public IQueryable<T> CreateQuery<T>() where T : ITableEntity, new()
        {
            return _table
                .CreateQuery<T>()
                .AsQueryable();
        }
    }
}
