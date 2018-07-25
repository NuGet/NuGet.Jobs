﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IStatusUpdater
    {
        /// <summary>
        /// Aggregates the information necessary to build a <see cref="ServiceStatus"/> that describes the NuGet service.
        /// </summary>
        Task Update();
    }
}
