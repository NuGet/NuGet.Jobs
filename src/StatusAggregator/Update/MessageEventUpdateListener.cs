// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    public class MessageEventUpdateListener : IComponentAffectingEntityUpdateListener<EventEntity>
    {
        private readonly TimeSpan _eventStartMessageDelay;

        private readonly ITableWrapper _table;
        private readonly IMessageBuilder _builder;

        private readonly ILogger<MessageEventUpdateListener> _logger;

        public MessageEventUpdateListener(
            ITableWrapper table,
            IMessageBuilder builder,
            StatusAggregatorConfiguration configuration,
            ILogger<MessageEventUpdateListener> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _builder = builder;
            _eventStartMessageDelay = TimeSpan.FromMinutes(configuration?.EventStartMessageDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnUpdate(EventEntity eventEntity, DateTime cursor)
        {
            using (_logger.Scope("Updating messages for event {EventRowKey}.", eventEntity.RowKey))
            {
                var linkedGroups = _table.GetLinkedEntities<IncidentGroupEntity, EventEntity>(eventEntity).ToList();
                var statusChanges = new List<StatusChange>();
                _logger.LogInformation("Event has {IncidentGroupsCount} linked incident groups.", linkedGroups.Count);
                foreach (var linkedGroup in linkedGroups)
                {
                    using (_logger.Scope("Getting status changes from incident group {IncidentGroupRowKey}.", linkedGroup.RowKey))
                    {
                        var path = linkedGroup.AffectedComponentPath;
                        var status = (ComponentStatus)linkedGroup.AffectedComponentStatus;
                        var startTime = linkedGroup.StartTime;
                        _logger.LogInformation("Incident group started at {StartTime}.", startTime);
                        statusChanges.Add(new StatusChange(startTime, path, status, MessageType.Start));
                        if (linkedGroup.EndTime.HasValue)
                        {
                            var endTime = linkedGroup.EndTime.Value;
                            _logger.LogInformation("Incident group ended at {EndTime}.", endTime);
                            statusChanges.Add(new StatusChange(endTime, path, status, MessageType.End));
                        }
                    }
                }

                var rootComponent = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent();
                DateTime? startMessageTimestamp = null;
                IComponent startMessageComponent = null;
                ComponentStatus startMessageStatus = ComponentStatus.Up;
                foreach (var statusChange in statusChanges.OrderBy(c => c.Timestamp))
                {
                    using (_logger.Scope("Processing status change of type {StatusChangeType} at {StatusChangeTimestamp} affecting {StatusChangePath} with status {StatusChangeStatus}.", 
                        statusChange.Type, statusChange.Timestamp, statusChange.AffectedComponentPath, statusChange.AffectedComponentStatus))
                    {
                        var component = rootComponent.GetByPath(statusChange.AffectedComponentPath);
                        switch (statusChange.Type)
                        {
                            case MessageType.Start:
                                _logger.LogInformation("Applying status change to component tree.");
                                component.Status = statusChange.AffectedComponentStatus;

                                var lowestVisibleComponent = GetLowestVisibleAffectedComponentPath(rootComponent, component);
                                if (lowestVisibleComponent == null || lowestVisibleComponent.Status == ComponentStatus.Up)
                                {
                                    _logger.LogInformation("Status change does not affect component tree. Will not post or edit any messages.");
                                    break;
                                }

                                if (startMessageTimestamp.HasValue)
                                {
                                    _logger.LogInformation("Found existing start message, will edit message with information from new status change.");
                                    var leastCommonAncestorPath = GetLeastCommonAncestor(startMessageComponent.Path, lowestVisibleComponent.Path);
                                    _logger.LogInformation("Least common ancestor component of existing start message is {LeastCommonAncestorPath}.", leastCommonAncestorPath);
                                    var leastCommonAncestor = rootComponent.GetByPath(leastCommonAncestorPath);
                                    if (leastCommonAncestor.Status == ComponentStatus.Up)
                                    {
                                        _logger.LogWarning("Least common ancestor of two visible components is unaffected!");
                                    }

                                    await _builder.UpdateMessage(eventEntity, startMessageTimestamp.Value, MessageType.Start, leastCommonAncestor);
                                    startMessageComponent = leastCommonAncestor;
                                    startMessageStatus = leastCommonAncestor.Status;
                                }
                                else
                                {
                                    _logger.LogInformation("Creating new start message for status change.");
                                    startMessageTimestamp = statusChange.Timestamp;
                                    startMessageComponent = lowestVisibleComponent;
                                    startMessageStatus = lowestVisibleComponent.Status;
                                    await _builder.CreateMessage(eventEntity, startMessageTimestamp.Value, statusChange.Type, startMessageComponent);
                                }

                                break;
                            case MessageType.End:
                                _logger.LogInformation("Removing status change from component tree.");
                                component.Status = ComponentStatus.Up;

                                if (startMessageTimestamp.HasValue && startMessageComponent.Status == ComponentStatus.Up)
                                {
                                    _logger.LogInformation("Found existing start message.");
                                    if (statusChange.Timestamp - startMessageTimestamp > _eventStartMessageDelay)
                                    {
                                        _logger.LogInformation("Existing start message is old enough to keep. Adding a new end message.");
                                        await _builder.CreateMessage(eventEntity, statusChange.Timestamp, statusChange.Type, startMessageComponent, startMessageStatus);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Existing start message is not old enough to keep. Deleting it.");
                                        await _builder.DeleteMessage(eventEntity, startMessageTimestamp.Value);
                                    }

                                    startMessageTimestamp = null;
                                    startMessageComponent = null;
                                }

                                break;
                            default:
                                _logger.LogWarning("Unexpected message type {MessageType}", statusChange.Type);
                                break;
                        }
                    }
                }

                if (startMessageTimestamp.HasValue && cursor - startMessageTimestamp.Value <= _eventStartMessageDelay)
                {
                    _logger.LogInformation("Remaining start message is not old enough to keep. Deleting it.");
                    await _builder.DeleteMessage(eventEntity, startMessageTimestamp.Value);
                }
            }
        }

        private IComponent GetLowestVisibleAffectedComponentPath(IComponent rootComponent, IComponent affectedComponent)
        {
            var pathParts = ComponentUtility.GetNames(affectedComponent.Path);
            for (var i = 1; i <= pathParts.Length; i++)
            {
                var path = ComponentUtility.GetPath(pathParts.Take(i).ToArray());
                var component = rootComponent.GetByPath(path);
                if (component == null)
                {
                    return affectedComponent;
                }

                if (!component.DisplaySubComponents)
                {
                    return component;
                }
            }

            return affectedComponent;
        }

        private string GetLeastCommonAncestor(string firstPath, string secondPath)
        {
            var firstPathParts = ComponentUtility.GetNames(firstPath);
            var secondPathParts = ComponentUtility.GetNames(secondPath);
            
            var maxPotentialCommonPathParts = new[] { firstPathParts, secondPathParts }.Min(p => p.Length);
            int commonPathParts;
            for (commonPathParts = 0; commonPathParts < maxPotentialCommonPathParts; commonPathParts++)
            {
                if (firstPathParts[commonPathParts] != secondPathParts[commonPathParts])
                {
                    break;
                }
            }

            return ComponentUtility.GetPath(firstPathParts.Take(commonPathParts).ToArray());
        }

        private class StatusChange
        {
            public DateTime Timestamp { get; }
            public string AffectedComponentPath { get; }
            public ComponentStatus AffectedComponentStatus { get; }
            public MessageType Type { get; }

            public StatusChange(DateTime timestamp, string affectedComponentPath, ComponentStatus affectedComponentStatus, MessageType type)
            {
                Timestamp = timestamp;
                AffectedComponentPath = affectedComponentPath;
                AffectedComponentStatus = affectedComponentStatus;
                Type = type;
            }
        }
    }
}
