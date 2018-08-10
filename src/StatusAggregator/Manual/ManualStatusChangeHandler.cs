using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class ManualStatusChangeHandler : IManualStatusChangeHandler
    {
        private readonly ITableWrapper _table;

        private IDictionary<ManualStatusChangeType, IManualStatusChangeProcessor> _processorForType;

        private readonly ILogger<ManualStatusChangeHandler> _logger;

        public ManualStatusChangeHandler(
            ITableWrapper table,
            IManualStatusChangeHandler<AddStatusEventManualChangeEntity> addStatusEventManualChangeHandler,
            IManualStatusChangeHandler<EditStatusEventManualChangeEntity> editStatusEventManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusEventManualChangeEntity> deleteStatusEventManualChangeHandler,
            IManualStatusChangeHandler<AddStatusMessageManualChangeEntity> addStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<EditStatusMessageManualChangeEntity> editStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity> deleteStatusMessageManualChangeHandler,
            ILogger<ManualStatusChangeHandler> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processorForType = new Dictionary<ManualStatusChangeType, IManualStatusChangeProcessor>
            {
                { ManualStatusChangeType.AddStatusEvent, new ManualStatusChangeProcessor<AddStatusEventManualChangeEntity>(this, addStatusEventManualChangeHandler) },
                { ManualStatusChangeType.EditStatusEvent, new ManualStatusChangeProcessor<EditStatusEventManualChangeEntity>(this, editStatusEventManualChangeHandler) },
                { ManualStatusChangeType.DeleteStatusEvent, new ManualStatusChangeProcessor<DeleteStatusEventManualChangeEntity>(this, deleteStatusEventManualChangeHandler) },
                { ManualStatusChangeType.AddStatusMessage, new ManualStatusChangeProcessor<AddStatusMessageManualChangeEntity>(this, addStatusMessageManualChangeHandler) },
                { ManualStatusChangeType.EditStatusMessage, new ManualStatusChangeProcessor<EditStatusMessageManualChangeEntity>(this, editStatusMessageManualChangeHandler) },
                { ManualStatusChangeType.DeleteStatusMessage, new ManualStatusChangeProcessor<DeleteStatusMessageManualChangeEntity>(this, deleteStatusMessageManualChangeHandler) }
            };
        }

        public async Task Handle(ManualStatusChangeEntity entity)
        {
            using (_logger.Scope("Handling manual status change at timestamp {ChangeTimestamp} with type {ChangeType}", entity.ChangeTimestamp, entity.Type.ToString()))
            {
                try
                {
                    var type = (ManualStatusChangeType)entity.Type;
                    if (_processorForType.ContainsKey(type))
                    {
                        await _processorForType[type].GetTask(entity);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid change type! Cannot process manual status change!", nameof(entity));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(LogEvents.ManualChangeFailure, e, "Failed to apply manual status change!");
                }
            }
        }

        private interface IManualStatusChangeProcessor
        {
            Task GetTask(ManualStatusChangeEntity entity);
        }

        private class ManualStatusChangeProcessor<T> : IManualStatusChangeProcessor
            where T : ManualStatusChangeEntity
        {
            private readonly ManualStatusChangeHandler _parent;
            private readonly IManualStatusChangeHandler<T> _handler;

            public ManualStatusChangeProcessor(ManualStatusChangeHandler parent, IManualStatusChangeHandler<T> handler)
            {
                _parent = parent;
                _handler = handler;
            }

            public async Task GetTask(ManualStatusChangeEntity entity)
            {
                var typedEntity = await _parent._table.RetrieveAsync<T>(entity.PartitionKey, entity.RowKey);
                await _handler.Handle(typedEntity);
            }
        }
    }
}
