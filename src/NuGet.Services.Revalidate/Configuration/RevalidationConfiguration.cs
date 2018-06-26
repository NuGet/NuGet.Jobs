// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    public class RevalidationConfiguration
    {
        public TimeSpan ShutdownWaitInterval { get; set; } = TimeSpan.FromDays(1);

        public int RevalidationQueueMaximumAttempts { get; set; } = 5;
        public TimeSpan RevalidationQueueSleepBetweenAttempts { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The configurations used to initialize the revalidation state.
        /// </summary>
        public InitializationConfiguration Initialization { get; set; }
    }
}
