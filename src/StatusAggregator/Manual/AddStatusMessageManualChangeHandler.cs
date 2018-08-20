﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class AddStatusMessageManualChangeHandler : IManualStatusChangeHandler<AddStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public AddStatusMessageManualChangeHandler(
            ITableWrapper table,
            ILogger<AddStatusMessageManualChangeEntity> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(AddStatusMessageManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);

            var messageEntity = new MessageEntity(
                eventRowKey,
                entity.ChangeTimestamp,
                entity.MessageContents);

            await _table.InsertAsync(messageEntity);

            var eventEntity = await _table.RetrieveAsync<EventEntity>(EventEntity.DefaultPartitionKey, eventRowKey);
            if (ManualStatusChangeUtility.UpdateEventIsActive(eventEntity, entity.EventIsActive, entity.ChangeTimestamp))
            {
                await _table.ReplaceAsync(eventEntity);
            }
        }
    }
}
