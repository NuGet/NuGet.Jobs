// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public interface IEventMessageExporter
    {
        /// <summary>
        /// Aggregates <see cref="MessageEntity"/>s associated with <paramref name="eventEntity"/> into <see cref="Event"/>s.
        /// </summary>
        IEnumerable<Event> Export(EventEntity eventEntity);
    }
}
