// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public class CurrentMessageExportContext
    {
        public EventEntity EventEntity { get; }
        public IEnumerable<Event> Events { get; }
        public IEnumerable<MessageEntity> UncommittedMessages { get; }

        public CurrentMessageExportContext(
            EventEntity eventEntity,
            IEnumerable<Event> events,
            IEnumerable<MessageEntity> uncommittedMessages)
        {
            EventEntity = eventEntity;
            Events = events;
            UncommittedMessages = uncommittedMessages;
        }

        public CurrentMessageExportContext(
            EventEntity eventEntity,
            IEnumerable<Event> events)
            : this(eventEntity, events, Enumerable.Empty<MessageEntity>())
        {
        }

        public CurrentMessageExportContext(EventEntity eventEntity)
            : this(eventEntity, Enumerable.Empty<Event>(), Enumerable.Empty<MessageEntity>())
        {
        }
    }
}
