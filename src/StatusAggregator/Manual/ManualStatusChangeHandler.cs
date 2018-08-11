﻿using System;
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
        private IDictionary<ManualStatusChangeType, IManualStatusChangeProcessor> _processorForType;

        private readonly ILogger<ManualStatusChangeHandler> _logger;

        public ManualStatusChangeHandler(
            IManualStatusChangeHandler<AddStatusEventManualChangeEntity> addStatusEventManualChangeHandler,
            IManualStatusChangeHandler<EditStatusEventManualChangeEntity> editStatusEventManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusEventManualChangeEntity> deleteStatusEventManualChangeHandler,
            IManualStatusChangeHandler<AddStatusMessageManualChangeEntity> addStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<EditStatusMessageManualChangeEntity> editStatusMessageManualChangeHandler,
            IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity> deleteStatusMessageManualChangeHandler,
            ILogger<ManualStatusChangeHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processorForType = new Dictionary<ManualStatusChangeType, IManualStatusChangeProcessor>
            {
                {
                    ManualStatusChangeType.AddStatusEvent,
                    new ManualStatusChangeProcessor<AddStatusEventManualChangeEntity>(addStatusEventManualChangeHandler)
                },

                {
                    ManualStatusChangeType.EditStatusEvent,
                    new ManualStatusChangeProcessor<EditStatusEventManualChangeEntity>(editStatusEventManualChangeHandler)
                },

                {
                    ManualStatusChangeType.DeleteStatusEvent,
                    new ManualStatusChangeProcessor<DeleteStatusEventManualChangeEntity>(deleteStatusEventManualChangeHandler)
                },

                {
                    ManualStatusChangeType.AddStatusMessage,
                    new ManualStatusChangeProcessor<AddStatusMessageManualChangeEntity>(addStatusMessageManualChangeHandler)
                },

                {
                    ManualStatusChangeType.EditStatusMessage,
                    new ManualStatusChangeProcessor<EditStatusMessageManualChangeEntity>(editStatusMessageManualChangeHandler)
                },

                {
                    ManualStatusChangeType.DeleteStatusMessage,
                    new ManualStatusChangeProcessor<DeleteStatusMessageManualChangeEntity>(deleteStatusMessageManualChangeHandler)
                }
            };
        }

        public async Task Handle(ITableWrapper table, ManualStatusChangeEntity entity)
        {
            using (_logger.Scope("Handling manual status change at timestamp {ChangeTimestamp} with type {ChangeType}", entity.ChangeTimestamp, entity.Type.ToString()))
            {
                try
                {
                    var type = (ManualStatusChangeType)entity.Type;
                    if (_processorForType.ContainsKey(type))
                    {
                        await _processorForType[type].GetTask(table, entity);
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
            Task GetTask(ITableWrapper table, ManualStatusChangeEntity entity);
        }

        private class ManualStatusChangeProcessor<T> : IManualStatusChangeProcessor
            where T : ManualStatusChangeEntity
        {
            private readonly IManualStatusChangeHandler<T> _handler;

            public ManualStatusChangeProcessor(IManualStatusChangeHandler<T> handler)
            {
                _handler = handler;
            }

            public async Task GetTask(ITableWrapper table, ManualStatusChangeEntity entity)
            {
                var typedEntity = await table.RetrieveAsync<T>(entity.PartitionKey, entity.RowKey);
                await _handler.Handle(typedEntity);
            }
        }
    }
}
