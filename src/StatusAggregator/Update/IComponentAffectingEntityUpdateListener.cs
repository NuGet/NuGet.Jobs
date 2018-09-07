// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    public interface IComponentAffectingEntityUpdateListener<T>
        where T : ComponentAffectingEntity
    {
        /// <summary>
        /// Called when <paramref name="entity"/> is updated by a <see cref="IComponentAffectingEntityUpdateHandler{T}"/>.
        /// </summary>
        Task OnUpdate(T entity, DateTime cursor);
    }
}