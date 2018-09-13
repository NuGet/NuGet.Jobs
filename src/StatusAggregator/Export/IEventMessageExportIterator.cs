// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public interface IEventMessageExportIterator
    {
        /// <summary>
        /// Processes <paramref name="message"/>.
        /// </summary>
        CurrentMessageExportContext Process(CurrentMessageExportContext context, MessageEntity message);

        /// <summary>
        /// Exports all messages that have been processed.
        /// </summary>
        IEnumerable<Event> Export(CurrentMessageExportContext context);
    }
}
