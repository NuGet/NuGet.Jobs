using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class DeleteStatusEventManualChangeHandler : IManualStatusChangeHandler<DeleteStatusEventManualChangeEntity>
    {
        private readonly ITableWrapper _table;
        private readonly ILogger<DeleteStatusEventManualChangeHandler> _logger;

        public DeleteStatusEventManualChangeHandler(
            ITableWrapper table,
            ILogger<DeleteStatusEventManualChangeHandler> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task Handle(DeleteStatusEventManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            return _table.DeleteAsync(EventEntity.DefaultPartitionKey, eventRowKey);
        }
    }
}
