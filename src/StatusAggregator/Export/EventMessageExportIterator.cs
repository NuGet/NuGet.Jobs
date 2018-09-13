// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public class EventMessageExportIterator : IEventMessageExportIterator
    {
        private readonly IEventMessageExportIterationHandler _handler;

        private readonly ILogger<EventMessageExportIterator> _logger;

        public EventMessageExportIterator(
            IEventMessageExportIterationHandler handler,
            ILogger<EventMessageExportIterator> logger)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CurrentMessageExportContext Process(CurrentMessageExportContext context, MessageEntity message)
        {
            var type = (MessageType)message.Type;
            using (_logger.Scope("Iterating through message {MessageRowKey} of type {MessageType}.", message.RowKey, type))
            {
                switch (type)
                {
                    case MessageType.Start:
                        context = _handler.CommitMessages(context, true);
                        return _handler.AddMessage(context, message);

                    case MessageType.End:
                        context = _handler.AddMessage(context, message);
                        return _handler.CommitMessages(context, true);

                    case MessageType.Manual:
                        return _handler.AddMessage(context, message);

                    default:
                        throw new ArgumentException(nameof(message));
                }
            }
        }

        public IEnumerable<Event> Export(CurrentMessageExportContext context)
        {
            using (_logger.Scope("Exporting all events from iterator."))
            {
                context = _handler.CommitMessages(
                    context, 
                    context.EventEntity.IsActive);

                return context.Events;
            }
        }
    }
}
