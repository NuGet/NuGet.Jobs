using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public class MessageChangeEventProcessor : IMessageChangeEventProcessor
    {
        private readonly IMessageFactory _factory;

        private readonly ILogger<MessageChangeEventProcessor> _logger;

        public MessageChangeEventProcessor(
            IMessageFactory factory,
            ILogger<MessageChangeEventProcessor> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CurrentMessageContext> Process(MessageChangeEvent change, EventEntity eventEntity, IComponent rootComponent, CurrentMessageContext context)
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
                            await _factory.CreateMessage(eventEntity, change.Timestamp, change.Type, lowestVisibleComponent);
                            return new CurrentMessageContext(change.Timestamp, lowestVisibleComponent, lowestVisibleComponent.Status);
                        }

                    case MessageType.End:
                        _logger.LogInformation("Removing change from component tree.");
                        component.Status = ComponentStatus.Up;

                        if (context != null)
                        {
                            _logger.LogInformation("Found existing message, testing if component tree is still affected.");

                            var affectedSubComponents = context.AffectedComponent.GetAllComponents();
                            if (affectedSubComponents.All(c => c.Status == ComponentStatus.Up))
                            {
                                _logger.LogInformation("Component tree is no longer affected. Creating end message.");
                                await _factory.CreateMessage(eventEntity, change.Timestamp, change.Type, context.AffectedComponent, context.AffectedComponentStatus);
                                return null;
                            }
                            else
                            {
                                _logger.LogInformation("Component tree is still affected. Will not post an end message.");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No existing message found. Will not add or delete any messages.");
                        }

                        return context;

                    default:
                        _logger.LogWarning("Unexpected message type {MessageType}", change.Type);
                        return context;
                }
            }
        }
    }
}
