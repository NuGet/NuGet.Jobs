// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>
    {
        Task<TAggregatedEntity> Create(ParsedIncident input, TEntityAggregation aggregation);
    }
}
