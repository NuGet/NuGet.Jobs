// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    /// <summary>
    /// Used by <see cref="EventMessageExportIterator"/> to handle operations during a single iteration.
    /// </summary>
    public interface IEventMessageExportIterationHandler
    {
        CurrentMessageExportContext AddMessage(CurrentMessageExportContext context, MessageEntity message);
        CurrentMessageExportContext CommitMessages(CurrentMessageExportContext context, bool isActive);
    }
}
