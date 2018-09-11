// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    public class ComponentAffectingEntityUpdater<T> : IComponentAffectingEntityUpdater, IComponentAffectingEntityUpdater<T>
        where T : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdateHandler<T> _handler;
        private readonly IEnumerable<IComponentAffectingEntityUpdateListener<T>> _listeners;

        private readonly ILogger<ComponentAffectingEntityUpdater<T>> _logger;

        public ComponentAffectingEntityUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdateHandler<T> handler,
            IEnumerable<IComponentAffectingEntityUpdateListener<T>> listeners,
            ILogger<ComponentAffectingEntityUpdater<T>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _listeners = listeners ?? throw new ArgumentNullException(nameof(listeners));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAllActive(DateTime cursor)
        {
            using (_logger.Scope("Updating active entities."))
            {
                var activeEntities = _table.GetActiveEntities<T>().ToList();
                _logger.LogInformation("Updating {ActiveEntitiesCount} active entities.", activeEntities.Count());
                foreach (var activeEntity in activeEntities)
                {
                    await _handler.Update(activeEntity, cursor);
                }
            }
        }

        public async Task<bool> Update(T entity, DateTime cursor)
        {
            var result = await _handler.Update(entity, cursor);
            foreach (var listener in _listeners)
            {
                await listener.OnUpdate(entity, cursor);
            }

            return result;
        }
    }
}
