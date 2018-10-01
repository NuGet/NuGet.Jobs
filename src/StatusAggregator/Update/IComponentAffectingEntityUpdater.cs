// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    public interface IComponentAffectingEntityUpdater
    {
        /// <summary>
        /// Updates entities with <see cref="IComponentAffectingEntity.IsActive"/> <c>true</c>.
        /// </summary>
        /// <param name="cursor">The current time.</param>
        Task UpdateAllActive(DateTime cursor);
    }

    public interface IComponentAffectingEntityUpdater<T>
        where T : ComponentAffectingEntity
    {
        /// <summary>
        /// Updates <paramref name="groupEntity"/> given that the current time is now <paramref name="cursor"/>.
        /// Returns whether <paramref name="groupEntity"/> is inactive.
        /// </summary>
        Task<bool> Update(T entity, DateTime cursor);
    }
}
