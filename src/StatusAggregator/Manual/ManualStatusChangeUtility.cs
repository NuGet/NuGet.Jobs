// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using System;

namespace StatusAggregator.Manual
{
    public static class ManualStatusChangeUtility
    {
        /// <summary>
        /// Compares <paramref name="eventEntity"/> to <paramref name="eventIsActive"/> and updates <paramref name="eventEntity"/> if they do not match.
        /// </summary>
        /// <param name="timestamp">A <see cref="DateTime"/> that represents when <paramref name="eventEntity"/> was closed if <paramref name="eventIsActive"/> is <c>false</c>.</param>
        /// <returns>
        /// Whether or not <paramref name="eventEntity"/> was updated.
        /// If true, the changes to <paramref name="eventEntity"/> should be saved to the table.
        /// </returns>
        public static bool UpdateEventIsActive(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
        {
            var shouldUpdateEvent = true;
            if (eventIsActive && eventEntity.EndTime != null)
            {
                throw new ArgumentException("An event cannot be reactivated!", nameof(eventIsActive));
            }
            else if (!eventIsActive && eventEntity.EndTime == null)
            {
                eventEntity.EndTime = timestamp;
            }
            else
            {
                shouldUpdateEvent = false;
            }

            return shouldUpdateEvent;
        }
    }
}
