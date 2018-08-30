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
    public class MessageBuilder : IMessageBuilder
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<MessageBuilder> _logger;

        public MessageBuilder(
            ITableWrapper table,
            ILogger<MessageBuilder> logger)
        {
            _table = table;
            _logger = logger;
        }

        public Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component)
        {
            return CreateMessage(eventEntity, time, type, component, component.Status);
        }

        public async Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status)
        {
            var existingMessage = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventEntity, time));
            if (existingMessage != null)
            {
                _logger.LogInformation("Message already exists, will not recreate.");
                return null;
            }

            if (!TryGetContentsForMessageHelper(type, component, status, out var contents))
            {
                _logger.LogWarning("Failed to get contents for new message!");
                return null;
            }

            var messageEntity = new MessageEntity(eventEntity, time, contents);
            _logger.LogInformation("Creating message with time {MessageTimestamp} and contents '{MessageContents}'.",
                messageEntity.Time, messageEntity.Contents);
            await _table.InsertAsync(messageEntity);
            return messageEntity;
        }

        public async Task UpdateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component)
        {
            var existingMessage = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventEntity, time));
            if (existingMessage == null)
            {
                _logger.LogInformation("Cannot update message that doesn't exist.");
                return;
            }

            if (!TryGetContentsForMessageHelper(type, component, out var newContents))
            {
                _logger.LogWarning("Failed to get contents to update message!");
                return;
            }

            _logger.LogInformation("Replacing contents of message with time {MessageTimestamp} and contents '{OldMessageContents}' with '{NewMessageContents}'.",
                existingMessage.Time, existingMessage.Contents, newContents);
            existingMessage.Contents = newContents;
            await _table.ReplaceAsync(existingMessage);
        }

        public Task DeleteMessage(EventEntity eventEntity, DateTime time)
        {
            return _table.DeleteAsync(TableUtility.GetPartitionKey<MessageEntity>(), MessageEntity.GetRowKey(eventEntity, time));
        }

        private bool TryGetContentsForMessageHelper(
            MessageType type,
            IComponent component,
            out string contents)
        {
            return TryGetContentsForMessageHelper(type, component, component.Status, out contents);
        }

        private bool TryGetContentsForMessageHelper(
            MessageType type,
            IComponent component,
            ComponentStatus status,
            out string contents)
        {
            return TryGetContentsForMessageHelper(type, component.Path, status, out contents);
        }

        private bool TryGetContentsForMessageHelper(
            MessageType type,
            string path,
            ComponentStatus status,
            out string contents)
        {
            contents = null;

            if (!_messageTypeToMessageTemplate.TryGetValue(type, out string messageTemplate))
            {
                _logger.LogWarning("Could not find a template for type {MessageType}.", type);
                return false;
            }

            var componentName = GetPrettyName(path);
            var actionDescription = GetActionDescriptionFromPath(path);
            if (actionDescription == null)
            {
                _logger.LogWarning("Could not find an action description for path {ComponentPath}.", path);
                return false;
            }

            var componentStatus = status.ToString().ToLowerInvariant();
            contents = string.Format(messageTemplate, componentName, componentStatus, actionDescription);

            return !string.IsNullOrEmpty(contents);
        }

        private string GetPrettyName(string path)
        {
            var componentNames = ComponentUtility.GetNames(path);
            return string.Join(" ", componentNames.Skip(1).Reverse());
        }

        private static readonly IDictionary<MessageType, string> _messageTypeToMessageTemplate = new Dictionary<MessageType, string>
        {
            { MessageType.Start, "**{0} is {1}.** You may encounter issues {2}." },
            { MessageType.End, "**{0} is no longer {1}.** You should no longer encounter any issues {2}. Thank you for your patience." },
        };

        private string GetActionDescriptionFromPath(string path)
        {
            return _actionDescriptionForComponentPathMap
                .FirstOrDefault(m => m.Matches(path))?
                .ActionDescription; ;
        }

        /// <remarks>
        /// This was not implemented as a dictionary because it is not possible to construct a <see cref="IEqualityComparer{T}.GetHashCode(T)"/> that works with component path prefixes.
        /// 
        /// Proof:
        /// A/B and A/C must have the same hashcode as A. (A/B and A/C are both prefixed by A.)
        /// However, A/B must not have the same hashcode as A/C. (A/B is not a prefix of A/C and vice versa.)
        /// Therefore, A/B and A/C must have a hashcode that is both identical AND different.
        /// This is not possible.
        /// </remarks>
        private static readonly IEnumerable<ActionDescriptionForComponentPathPrefix> _actionDescriptionForComponentPathMap = new ActionDescriptionForComponentPathPrefix[]
        {
            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName),
                $"browsing the NuGet Gallery"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName),
                $"restoring packages from NuGet.org's V3 feed from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName),
                $"restoring packages from NuGet.org's V3 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName),
                $"restoring packages from NuGet.org's V2 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName),
                $"restoring packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName),
                $"searching for packages from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName),
                $"searching for packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName),
                "uploading new packages"),
        };

        private class ActionDescriptionForComponentPathPrefix
        {
            public string ComponentPathPrefix { get; }
            public string ActionDescription { get; }

            public ActionDescriptionForComponentPathPrefix(string componentPathPrefix, string actionDescription)
            {
                ComponentPathPrefix = componentPathPrefix;
                ActionDescription = actionDescription;
            }

            public bool Matches(string componentPath)
            {
                return componentPath.StartsWith(ComponentPathPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
