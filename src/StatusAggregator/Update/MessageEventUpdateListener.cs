// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;

namespace StatusAggregator.Update
{
    public class MessageEventUpdateListener : IComponentAffectingEntityUpdateListener<EventEntity>
    {
        private readonly IMessageChangeEventProvider _provider;
        private readonly IMessageChangeEventIterator _iterator;

        private readonly ILogger<MessageEventUpdateListener> _logger;

        public MessageEventUpdateListener(
            IMessageChangeEventProvider provider,
            IMessageChangeEventIterator iterator,
            ILogger<MessageEventUpdateListener> logger)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _iterator = iterator ?? throw new ArgumentNullException(nameof(iterator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task OnUpdate(EventEntity eventEntity, DateTime cursor)
        {
            using (_logger.Scope("Updating messages for event {EventRowKey}.", eventEntity.RowKey))
            {
                var changes = _provider.Get(eventEntity);
                return _iterator.Iterate(changes, eventEntity, cursor);
            }
        }
    }
}
