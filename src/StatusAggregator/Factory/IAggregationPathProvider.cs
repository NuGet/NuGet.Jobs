// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;

namespace StatusAggregator.Factory
{
    public interface IAggregationPathProvider<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        /// <summary>
        /// Returns the component path under which a <typeparamref name="TAggregatedEntity"/> represented by <paramref name="input"/> should be aggregated into a <typeparamref name="TEntityAggregation"/> by.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        string Get(IComponentAffectingEntity input);
    }
}
