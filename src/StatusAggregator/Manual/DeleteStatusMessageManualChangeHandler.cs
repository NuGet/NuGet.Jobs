using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class DeleteStatusMessageManualChangeHandler : IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public DeleteStatusMessageManualChangeHandler(
            ITableWrapper table,
            ILogger<DeleteStatusMessageManualChangeHandler> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public Task Handle(DeleteStatusMessageManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var messageRowKey = MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp);
            return _table.DeleteAsync(MessageEntity.DefaultPartitionKey, messageRowKey);
        }
    }
}
