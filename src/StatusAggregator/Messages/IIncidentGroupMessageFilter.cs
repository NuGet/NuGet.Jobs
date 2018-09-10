// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IIncidentGroupMessageFilter
    {
        /// <summary>
        /// Returns whether or not messages should be posted about <paramref name="group"/>.
        /// </summary>
        bool CanPostMessages(IncidentGroupEntity group);
    }
}
