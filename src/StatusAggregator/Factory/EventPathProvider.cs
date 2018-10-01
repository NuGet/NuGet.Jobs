// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public class EventPathProvider : IAffectedComponentPathProvider<EventEntity>
    {
        /// <summary>
        /// <see cref="IncidentGroupEntity"/>s are aggregated into <see cref="EventEntity"/>s by the path of its level 1 ancestor.
        /// </summary>
        public string Get(ParsedIncident input)
        {
            var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
            var topLevelComponentPathParts = pathParts.Take(2).ToArray();
            return ComponentUtility.GetPath(topLevelComponentPathParts);
        }
    }
}
