using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class EditStatusMessageManualChangeHandler : IManualStatusChangeHandler<EditStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;
        private readonly ILogger<EditStatusMessageManualChangeHandler> _logger;

        public EditStatusMessageManualChangeHandler(
            ITableWrapper table,
            ILogger<EditStatusMessageManualChangeHandler> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(EditStatusMessageManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var messageEntity = await _table.RetrieveAsync<MessageEntity>(
                MessageEntity.DefaultPartitionKey,
                MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp));

            messageEntity.Contents = entity.MessageContents;

            await _table.ReplaceAsync(messageEntity);
        }
    }
}
