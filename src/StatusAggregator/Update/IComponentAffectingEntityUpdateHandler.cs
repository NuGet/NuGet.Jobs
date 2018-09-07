// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    /// <summary>
    /// Wrapped by <see cref="IComponentAffectingEntityUpdater{T}"/> to update <typeparamref name="T"/>s.
    /// </summary>
    public interface IComponentAffectingEntityUpdateHandler<T>
        where T : ComponentAffectingEntity
    {
        /// <summary>
        /// Updates <paramref name="groupEntity"/> given that the current time is now <paramref name="cursor"/>.
        /// Returns whether <paramref name="groupEntity"/> is inactive.
        /// </summary>
        Task<bool> Update(T groupEntity, DateTime cursor);
    }
}