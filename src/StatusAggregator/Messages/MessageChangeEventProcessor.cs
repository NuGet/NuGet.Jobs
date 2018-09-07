using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public class MessageChangeEventProcessor : IMessageChangeEventProcessor
    {
        private readonly TimeSpan _eventStartMessageDelay;

        private readonly IMessageFactory _factory;

        private readonly ILogger<MessageChangeEventProcessor> _logger;

        public MessageChangeEventProcessor(
            IMessageFactory factory,
            StatusAggregatorConfiguration configuration,
            ILogger<MessageChangeEventProcessor> logger)
        {
            _factory = factory;
            _eventStartMessageDelay = TimeSpan.FromMinutes(configuration?.EventStartMessageDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger;
        }

        public async Task<CurrentMessageContext> Process(MessageChangeEvent change, DateTime cursor, EventEntity eventEntity, IComponent rootComponent, CurrentMessageContext context)
        {
            using (_logger.Scope("Processing change of type {StatusChangeType} at {StatusChangeTimestamp} affecting {StatusChangePath} with status {StatusChangeStatus}.",
                change.Type, change.Timestamp, change.AffectedComponentPath, change.AffectedComponentStatus))
            {
                var component = rootComponent.GetByPath(change.AffectedComponentPath);
                switch (change.Type)
                {
                    case MessageType.Start:
                        _logger.LogInformation("Applying change to component tree.");
                        component.Status = change.AffectedComponentStatus;

                        var lowestVisibleComponent = rootComponent.GetLeastCommonVisibleAncestorOfSubComponent(component);
                        if (lowestVisibleComponent == null || lowestVisibleComponent.Status == ComponentStatus.Up)
                        {
                            _logger.LogInformation("Change does not affect component tree. Will not post or edit any messages.");
                            return context;
                        }

                        if (context != null)
                        {
                            _logger.LogInformation("Found existing message, will edit it with information from new change.");
                            var leastCommonAncestorPath = ComponentUtility.GetLeastCommonAncestorPath(context.AffectedComponent, lowestVisibleComponent);
                            _logger.LogInformation("Least common ancestor component of existing message and this change is {LeastCommonAncestorPath}.", leastCommonAncestorPath);
                            var leastCommonAncestor = rootComponent.GetByPath(leastCommonAncestorPath);
                            if (leastCommonAncestor == null)
                            {
                                _logger.LogWarning(
                                    "Least common ancestor component of existing message and this change is not a part of the component tree. " +
                                    "Will not edit the existing message.");
                                return context;
                            }

                            if (leastCommonAncestor.Status == ComponentStatus.Up)
                            {
                                _logger.LogWarning("Least common ancestor of two visible components is unaffected!");
                            }

                            await _factory.UpdateMessage(eventEntity, context.Timestamp, MessageType.Start, leastCommonAncestor);
                            return new CurrentMessageContext(context.Timestamp, leastCommonAncestor, leastCommonAncestor.Status);
                        }
                        else
                        {
                            _logger.LogInformation("No existing message found. Creating new start message for change.");

                            if (cursor - change.Timestamp <= _eventStartMessageDelay)
                            {
                                _logger.LogInformation("Change is too new to post.");
                                return context;
                            }

                            await _factory.CreateMessage(eventEntity, change.Timestamp, change.Type, lowestVisibleComponent);
                            return new CurrentMessageContext(change.Timestamp, lowestVisibleComponent, lowestVisibleComponent.Status);
                        }

                    case MessageType.End:
                        _logger.LogInformation("Removing change from component tree.");
                        component.Status = ComponentStatus.Up;

                        if (context != null && context.AffectedComponent.Status == ComponentStatus.Up)
                        {
                            _logger.LogInformation("Existing message is affected by change!");
                            if (change.Timestamp - context.Timestamp > _eventStartMessageDelay)
                            {
                                _logger.LogInformation("Existing message is old enough to keep. Adding a new end message.");
                                await _factory.CreateMessage(eventEntity, change.Timestamp, change.Type, context.AffectedComponent, context.AffectedComponentStatus);
                            }
                            else
                            {
                                _logger.LogInformation("Existing message is not old enough to keep. Deleting it.");
                                await _factory.DeleteMessage(eventEntity, context.Timestamp);
                            }

                            return null;
                        }
                        else
                        {
                            _logger.LogInformation("Change has not affected component tree. Will not add or delete any messages.");
                            return context;
                        }

                    default:
                        _logger.LogWarning("Unexpected message type {MessageType}", change.Type);
                        return context;
                }
            }
        }
    }
}
