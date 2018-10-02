// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAggregationManager<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : IAggregatedEntity<TEntityAggregation>
        where TEntityAggregation : IComponentAffectingEntity
    {
        /// <summary>
        /// Determines if an entity built from <paramref name="input"/> using a <see cref="IComponentAffectingEntityFactory{TEntity}"/> can be aggregated by <paramref name="entityAggregation"/>.
        /// </summary>
        Task<bool> CanBeAggregatedBy(ParsedIncident input, TEntityAggregation entityAggregation);
    }
}
