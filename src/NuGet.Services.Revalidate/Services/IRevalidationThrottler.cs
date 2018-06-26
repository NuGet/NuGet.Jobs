// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public interface IRevalidationThrottler
    {
        /// <summary>
        /// Check whether the revalidation capacity has been reached.
        /// </summary>
        /// <returns>If true, no more revalidations should be performed.</returns>
        Task<bool> IsThrottledAsync();

        /// <summary>
        /// Reset the capacity to the configured minimum value. Call this when the service's status is degraded to
        /// throttle the revalidations.
        /// </summary>
        /// <returns>A task that completes once the capacity theshold has been reset.</returns>
        Task ResetCapacityAsync();

        /// <summary>
        /// Increase the revalidation capacity by one revalidation per minute.
        /// </summary>
        /// <returns>A task taht completes once the capacity has been increased.</returns>
        Task IncreaseCapacityAsync();

        /// <summary>
        /// Calculate the time that should slept between revalidations to achieve the desired revalidation rate.
        /// </summary>
        /// <returns>The time that should be slept between revalidations.</returns>
        TimeSpan RevalidationSleepInterval();
    }
}
