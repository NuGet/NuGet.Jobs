// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Status;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Describes the last message posted for use by <see cref="IMessageChangeEventIterator"/> while iterating through messages.
    /// </summary>
    public class CurrentMessageContext
    {
        public DateTime Timestamp { get; }
        public IComponent AffectedComponent { get; }
        public ComponentStatus AffectedComponentStatus { get; }

        public CurrentMessageContext(
            DateTime timestamp, 
            IComponent affectedComponent, 
            ComponentStatus affectedComponentStatus)
        {
            Timestamp = timestamp;
            AffectedComponent = affectedComponent;
            AffectedComponentStatus = affectedComponentStatus;
        }
    }
}
