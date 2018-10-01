// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAggregationProvider<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : IAggregatedEntity<TEntityAggregation>
        where TEntityAggregation : IComponentAffectingEntity
    {
        /// <summary>
        /// Gets an aggregation that matches <paramref name="input"/>.
        /// </summary>
        Task<TEntityAggregation> Get(ParsedIncident input);
    }
}
