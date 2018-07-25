using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;


namespace StatusAggregator
{
    public class Cursor : ICursor
    {
        public Cursor(
            ITableWrapper table,
            ILogger<Cursor> logger)
        {
            _table = table;
            _logger = logger;
        }

        private readonly ITableWrapper _table;

        private readonly ILogger<Cursor> _logger;

        public async Task<DateTime> Get()
        {
            var cursor = await _table.Retrieve<CursorEntity>(
                CursorEntity.DefaultPartitionKey, CursorEntity.DefaultRowKey);

            DateTime value;
            if (cursor == null)
            {
                value = DateTime.MinValue;
                _logger.LogInformation("Could not fetch cursor.");
            }
            else
            {
                value = cursor.Value;
                _logger.LogInformation("Fetched cursor with value {Cursor}.", value);
            }

            return value;
        }

        public Task Set(DateTime value)
        {
            var cursorEntity = new CursorEntity(value);
            return _table.InsertOrReplaceAsync(cursorEntity);
        }
    }
}
