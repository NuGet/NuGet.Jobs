// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public class RevalidationThrottler : IRevalidationThrottler
    {
        public Task<bool> IsThrottledAsync()
        {
            // TODO:
            // Calculate desired event rate
            // Calculate current event rate (# of revalidations + Gallery actions)
            // Compare desired event rate to configured event rate. If configured rate is higher, update desired event rate.
            // If current event rate is greater than or equal to desired event rate, return true;
            return Task.FromResult(false);
        }

        public Task ResetCapacityAsync()
        {
            return Task.CompletedTask;
        }

        public Task IncreaseCapacityAsync()
        {
            return Task.CompletedTask;
        }

        public TimeSpan RevalidationSleepInterval()
        {
            return TimeSpan.FromMinutes(5);
        }
    }
}
