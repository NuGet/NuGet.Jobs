// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Messages
{
    public class MessageFactory : IMessageFactory
    {
        private readonly ITableWrapper _table;
        private readonly IMessageContentBuilder _builder;

        private readonly ILogger<MessageFactory> _logger;

        public MessageFactory(
            ITableWrapper table,
            IMessageContentBuilder builder,
            ILogger<MessageFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component)
        {
            return CreateMessage(eventEntity, time, type, component, component.Status);
        }

        public async Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status)
        {
            using (_logger.Scope("Creating new message of type {Type} for event {EventRowKey} at {Timestamp} affecting {ComponentPath} with status {ComponentStatus}.",
                type, eventEntity.RowKey, time, component.Path, status))
            {
                var existingMessage = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventEntity, time));
                if (existingMessage != null)
                {
                    _logger.LogInformation("Message already exists, will not recreate.");
                    return existingMessage;
                }

                if (!_builder.TryGetContentsForMessageHelper(type, component, status, out var contents))
                {
                    _logger.LogWarning("Failed to get contents for new message!");
                    return null;
                }

                var messageEntity = new MessageEntity(eventEntity, time, contents, type);
                _logger.LogInformation("Creating message with time {MessageTimestamp} and contents {MessageContents}.",
                    messageEntity.Time, messageEntity.Contents);
                await _table.InsertAsync(messageEntity);
                return messageEntity;
            }
        }

        public async Task UpdateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component)
        {
            using (_logger.Scope("Updating existing message of type {Type} for event {EventRowKey} at {Timestamp} affecting {ComponentPath}.",
                type, eventEntity.RowKey, time, component.Path))
            {
                var existingMessage = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventEntity, time));
                if (existingMessage == null)
                {
                    _logger.LogWarning("Cannot update message that doesn't exist.");
                    return;
                }

                var existingMessageType = (MessageType)existingMessage.Type;
                if (existingMessageType != type)
                {
                    if (existingMessageType == MessageType.Manual)
                    {
                        _logger.LogInformation("Message was changed manually, cannot update.");
                    }
                    else
                    {
                        _logger.LogWarning("Cannot update message, has unexpected type {UnexpectedType}.", existingMessageType);
                    }

                    return;
                }

                if (!_builder.TryGetContentsForMessageHelper(type, component, out var newContents))
                {
                    _logger.LogWarning("Failed to get contents to update message!");
                    return;
                }

                _logger.LogInformation("Replacing contents of message with time {MessageTimestamp} and contents {OldMessageContents} with {NewMessageContents}.",
                    existingMessage.Time, existingMessage.Contents, newContents);
                existingMessage.Contents = newContents;
                await _table.ReplaceAsync(existingMessage);
            }
        }

        public Task DeleteMessage(EventEntity eventEntity, DateTime time)
        {
            return _table.DeleteAsync(TableUtility.GetPartitionKey<MessageEntity>(), MessageEntity.GetRowKey(eventEntity, time));
        }
    }
}
