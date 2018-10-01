// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Factory
{
    public interface IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>
    {
        /// <summary>
        /// Called when <paramref name="aggregatedEntity"/> is linked to <paramref name="entityAggregation"/>.
        /// </summary>
        Task OnLink(TEntityAggregation entityAggregation, TAggregatedEntity aggregatedEntity);
    }
}
