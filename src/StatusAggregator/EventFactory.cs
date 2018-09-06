// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EventFactory : IEntityFactory<EventEntity>
    {
        private readonly ITableWrapper _table;

        public EventFactory(ITableWrapper table)
        {
            _table = table;
        }

        public async Task<EventEntity> Create(ParsedIncident input)
        {
            var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
            var topLevelComponentPathParts = pathParts.Take(2).ToArray();
            var path = ComponentUtility.GetPath(topLevelComponentPathParts);
            var entity = new EventEntity(path, input.StartTime);
            await _table.InsertOrReplaceAsync(entity);
            return entity;
        }
    }
}
