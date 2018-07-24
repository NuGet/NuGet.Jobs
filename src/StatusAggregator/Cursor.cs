using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;


namespace StatusAggregator
{
    public class Cursor : ICursor
    {
        public Cursor(ITableWrapper table)
        {
            _table = table;
        }

        private ITableWrapper _table;

        public async Task<DateTime> Get()
        {
            var cursor = await _table.Retrieve<CursorEntity>(
                CursorEntity.DefaultPartitionKey, CursorEntity.DefaultRowKey);

            return cursor != null
                ? cursor.Value
                : DateTime.MinValue;
        }

        public Task Set(DateTime value)
        {
            var cursorEntity = new CursorEntity(value);
            return _table.InsertOrReplaceAsync(cursorEntity);
        }
    }
}
