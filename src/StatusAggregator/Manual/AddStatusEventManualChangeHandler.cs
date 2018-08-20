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
    public class AddStatusEventManualChangeHandler : IManualStatusChangeHandler<AddStatusEventManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public AddStatusEventManualChangeHandler(
            ITableWrapper table,
            ILogger<AddStatusEventManualChangeHandler> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(AddStatusEventManualChangeEntity entity)
        {
            var time = entity.ChangeTimestamp;

            var eventEntity = new EventEntity(
                entity.EventAffectedComponentPath ?? throw new ArgumentNullException($"{nameof(entity)}.{nameof(entity.EventAffectedComponentPath)}"),
                entity.EventAffectedComponentStatus,
                time,
                entity.EventIsActive ? (DateTime?)null : time);

            var messageEntity = new MessageEntity(
                eventEntity,
                time,
                entity.MessageContents ?? throw new ArgumentNullException($"{nameof(entity)}.{nameof(entity.MessageContents)}"));

            await _table.InsertAsync(messageEntity);
            await _table.InsertAsync(eventEntity);
        }
    }
}
