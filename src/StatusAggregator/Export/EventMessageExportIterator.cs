// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public class EventMessageExportIterator : IEventMessageExportIterator
    {
        private readonly EventEntity _eventEntity;

        private readonly IList<Event> _events = new List<Event>();
        private readonly IList<MessageEntity> _currentMessages = new List<MessageEntity>();

        private readonly ILogger<EventMessageExportIterator> _logger;

        public EventMessageExportIterator(
            EventEntity eventEntity,
            ILogger<EventMessageExportIterator> logger)
        {
            _eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<Event> Export()
        {
            using (_logger.Scope("Exporting all events from iterator."))
            {
                CommitMessages(!_eventEntity.IsActive);
                return _events;
            }
        }

        public void Process(MessageEntity message)
        {
            var type = (MessageType)message.Type;
            using (_logger.Scope("Iterating through message {MessageRowKey} of type {MessageType}.", message.RowKey, type))
            {
                switch (type)
                {
                    case MessageType.Start:
                        CommitMessages(true);
                        AddMessageInternal(message);

                        break;

                    case MessageType.End:
                        AddMessageInternal(message);
                        CommitMessages(true);

                        break;

                    case MessageType.Manual:
                        AddMessageInternal(message);

                        break;
                }
            }
        }

        private void AddMessageInternal(MessageEntity message)
        {
            _logger.LogInformation("Adding message to cache.");
            _currentMessages.Add(message);
        }

        private void CommitMessages(bool hasEndTime)
        {
            using (_logger.Scope("Creating event from message cache."))
            {
                if (_currentMessages.Any())
                {
                    var startTime = _currentMessages.Min(m => m.Time);
                    var endTime = hasEndTime
                        ? _currentMessages.Max(m => m.Time)
                        : (DateTime?)null;

                    var newEvent = new Event(
                        _eventEntity.AffectedComponentPath,
                        startTime,
                        endTime,
                        _currentMessages
                            .Select(m => new Message(m.Time, m.Contents))
                            .ToList());

                    _logger.LogInformation("Created event affects {Path} from {StartTime} to {EndTime} and contains {MessageCount} messages.",
                        newEvent.AffectedComponentPath, newEvent.StartTime, newEvent.EndTime, newEvent.Messages.Count());

                    _events.Add(newEvent);

                    _logger.LogInformation("Clearing message cache.");
                    _currentMessages.Clear();
                }
                else
                {
                    _logger.LogInformation("Message cache is empty! Cannot create event with empty cache.");
                }
            }
        }
    }
}
