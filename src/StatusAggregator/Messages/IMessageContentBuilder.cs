// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Used by <see cref="MessageFactory"/> to build content for <see cref="MessageEntity"/>s.
    /// </summary>
    public interface IMessageContentBuilder
    {
        /// <summary>
        /// Tries to get contents for a message of type <paramref name="type"/> affecting <paramref name="component"/>.
        /// </summary>
        /// <param name="contents">The content of the message.</param>
        /// <returns>
        /// True if contents for the message can be generated.
        /// False otherwise.
        /// </returns>
        bool TryGetContentsForMessageHelper(MessageType type, IComponent component, out string contents);

        /// <summary>
        /// Tries to get contents for a message of type <paramref name="type"/> affecting <paramref name="component"/> with status <paramref name="status"/>.
        /// </summary>
        /// <param name="contents">The content of the message.</param>
        /// <returns>
        /// True if contents for the message can be generated.
        /// False otherwise.
        /// </returns>
        bool TryGetContentsForMessageHelper(MessageType type, IComponent component, ComponentStatus status, out string contents);
    }
}