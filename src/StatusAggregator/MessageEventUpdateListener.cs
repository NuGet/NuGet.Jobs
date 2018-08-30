// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
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
            var linkedGroups = _table.GetLinkedEntities<IncidentGroupEntity>(eventEntity).ToList();
            var statusChanges = new List<StatusChange>();
            foreach (var linkedGroup in linkedGroups)
            {
                var path = linkedGroup.AffectedComponentPath;
                var status = (ComponentStatus)linkedGroup.AffectedComponentStatus;
                statusChanges.Add(new StatusChange(linkedGroup.StartTime, path, status, MessageType.Start));
                if (linkedGroup.EndTime.HasValue)
                {
                    statusChanges.Add(new StatusChange(linkedGroup.EndTime.Value, path, status, MessageType.End));
                }
            }

            var rootComponent = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent();
            DateTime? startMessageTimestamp = null;
            IComponent startMessageComponent = null;
            ComponentStatus startMessageStatus = ComponentStatus.Up;
            foreach (var statusChange in statusChanges.OrderBy(c => c.Timestamp))
            {
                var component = rootComponent.GetByPath(statusChange.AffectedComponentPath);
                switch (statusChange.Type)
                {
                    case MessageType.Start:
                        component.Status = statusChange.AffectedComponentStatus;
                        
                        var lowestVisibleComponent = GetLowestVisibleAffectedComponentPath(rootComponent, component);
                        if (lowestVisibleComponent == null || lowestVisibleComponent.Status == ComponentStatus.Up)
                        {
                            break;
                        }

                        if (startMessageTimestamp.HasValue)
                        {
                            var leastCommonAncestorPath = GetLeastCommonAncestor(startMessageComponent.Path, lowestVisibleComponent.Path);
                            var leastCommonAncestor = rootComponent.GetByPath(leastCommonAncestorPath);
                            if (leastCommonAncestor.Status == ComponentStatus.Up)
                            {
                                _logger.LogWarning("Least common ancestor of two visible components is not visible!");
                            }

                            await _builder.UpdateMessage(eventEntity, startMessageTimestamp.Value, MessageType.Start, leastCommonAncestor);
                            startMessageComponent = leastCommonAncestor;
                            startMessageStatus = leastCommonAncestor.Status;
                        }
                        else
                        {
                            startMessageTimestamp = statusChange.Timestamp;
                            startMessageComponent = lowestVisibleComponent;
                            startMessageStatus = lowestVisibleComponent.Status;
                            await _builder.CreateMessage(eventEntity, startMessageTimestamp.Value, statusChange.Type, startMessageComponent);
                        }

                        break;
                    case MessageType.End:
                        component.Status = ComponentStatus.Up;

                        if (startMessageTimestamp.HasValue && startMessageComponent.Status == ComponentStatus.Up)
                        {
                            if (statusChange.Timestamp - startMessageTimestamp > _eventStartMessageDelay)
                            {
                                await _builder.CreateMessage(eventEntity, statusChange.Timestamp, statusChange.Type, startMessageComponent, startMessageStatus);
                            }
                            else
                            {
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

            if (cursor - startMessageTimestamp <= _eventStartMessageDelay)
            {
                await _builder.DeleteMessage(eventEntity, startMessageTimestamp.Value);
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
