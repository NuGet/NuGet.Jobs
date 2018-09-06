// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IComponentAffectingEntityUpdateHandler<T>
        where T : ComponentAffectingEntity
    {
        Task<bool> Update(T groupEntity, DateTime cursor);
    }
}