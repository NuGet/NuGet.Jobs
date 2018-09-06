// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IMessageBuilder
    {
        Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);

        Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status);

        Task UpdateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);

        Task DeleteMessage(EventEntity eventEntity, DateTime time);
    }
}
