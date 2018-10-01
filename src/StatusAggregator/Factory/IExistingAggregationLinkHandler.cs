// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : IAggregatedEntity<TEntityAggregation>
        where TEntityAggregation : IComponentAffectingEntity
    {
        /// <summary>
        /// Determines if an entity built from <paramref name="input"/> using a <see cref="IEntityFactory{TEntity}"/> can be linked to <paramref name="entityAggregation"/>.
        /// </summary>
        Task<bool> CanLink(ParsedIncident input, TEntityAggregation entityAggregation);
    }
}
