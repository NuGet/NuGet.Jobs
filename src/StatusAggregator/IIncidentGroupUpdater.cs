// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    /// <summary>
    /// Handles updating any active <see cref="EventEntity"/>s.
    /// </summary>
    public interface IIncidentGroupUpdater
    {
        /// <summary>
        /// Updates all active <see cref="EventEntity"/>s.
        /// </summary>
        /// <param name="cursor">The current timestamp processed by the job.</param>
        Task UpdateActiveIncidentGroups(DateTime cursor);

        /// <summary>
        /// Update <paramref name="groupEntity"/> given <paramref name="cursor"/>.
        /// Determines whether or not to deactivate <paramref name="groupEntity"/> and updates any messages associated with the event.
        /// </summary>
        /// <param name="cursor">The current timestamp processed by the job.</param>
        /// <returns>Whether or not <paramref name="groupEntity"/> was deactivated.</returns>
        Task<bool> UpdateIncidentGroup(IncidentGroupEntity groupEntity, DateTime cursor);
    }
}
