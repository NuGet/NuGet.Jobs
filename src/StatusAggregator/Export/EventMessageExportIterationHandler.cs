// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public class EventMessageExportIterationHandler : IEventMessageExportIterationHandler
    {
        private readonly ILogger<EventMessageExportIterationHandler> _logger;

        public EventMessageExportIterationHandler(ILogger<EventMessageExportIterationHandler> logger)
        {
            _logger = logger;
        }

        public CurrentMessageExportContext AddMessage(CurrentMessageExportContext context, MessageEntity message)
        {
            _logger.LogInformation("Adding message to uncommitted messages in context.");
            return new CurrentMessageExportContext(
                context.EventEntity, 
                context.Events, 
                context.UncommittedMessages.Concat(new[] { message }));
        }

        public CurrentMessageExportContext CommitMessages(CurrentMessageExportContext context, bool isActive)
        {
            using (_logger.Scope("Creating event from uncommitted messages with isActive {isActive}.", isActive))
            {
                if (context.UncommittedMessages.Any())
                {
                    var startTime = context.UncommittedMessages.Min(m => m.Time);
                    var endTime = isActive
                        ? (DateTime?)null
                        : context.UncommittedMessages.Max(m => m.Time);

                    var newEvent = new Event(
                        context.EventEntity.AffectedComponentPath,
                        startTime,
                        endTime,
                        context.UncommittedMessages
                            .OrderBy(m => m.Time)
                            .Select(m => new Message(m.Time, m.Contents))
                            .ToList());

                    _logger.LogInformation("Created event affects {Path} from {StartTime} to {EndTime} and contains {MessageCount} messages.",
                        newEvent.AffectedComponentPath, newEvent.StartTime, newEvent.EndTime, newEvent.Messages.Count());
                    _logger.LogInformation("Clearing uncommited messages.");
                    return new CurrentMessageExportContext(
                        context.EventEntity,
                        context.Events.Concat(new[] { newEvent }));
                }
                else
                {
                    _logger.LogInformation("There are no uncommitted messages! Cannot create event without uncommitted messages.");
                    return context;
                }
            }
        }
    }
}
