using System;
using System.Linq;
using System.Threading.Tasks;
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

        public DateTime Get()
        {
            var cursors = _table
                .CreateQuery<CursorEntity>()
                .Where(e => e.PartitionKey == CursorEntity.DefaultPartitionKey)
                .ToList();

            return cursors.Any()
                ? cursors.Max(c => c.Value)
                : DateTime.MinValue;
        }

        public Task Set(DateTime value)
        {
            var cursorEntity = new CursorEntity(value);
            return _table.InsertOrReplaceAsync(cursorEntity);
        }
    }
}
