// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;

namespace StatusAggregator.Factory
{
    public class IncidentAggregationPathProvider : IAggregationPathProvider<IncidentEntity, IncidentGroupEntity>
    {
        /// <summary>
        /// <see cref="IncidentEntity"/>s are aggregated into <see cref="IncidentGroupEntity"/>s by the same path.
        /// </summary>
        public string Get(IComponentAffectingEntity input)
        {
            return input.AffectedComponentPath;
        }
    }
}
