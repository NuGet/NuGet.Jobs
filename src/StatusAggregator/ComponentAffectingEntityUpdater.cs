using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public class ComponentAffectingEntityUpdater<T> : IComponentAffectingEntityUpdater, IComponentAffectingEntityUpdater<T>
        where T : ITableEntity, IComponentAffectingEntity, new()
    {
        private readonly IComponentAffectingEntityUpdateHandler _handler;
        private readonly IComponentAffectingEntityUpdateHandler<T> _handlerT;
        private readonly IEnumerable<IComponentAffectingEntityUpdateListener<T>> _listeners;

        public ComponentAffectingEntityUpdater(
            IComponentAffectingEntityUpdateHandler handler,
            IComponentAffectingEntityUpdateHandler<T> handlerT,
            IEnumerable<IComponentAffectingEntityUpdateListener<T>> listeners)
        {
            _handler = handler;
            _handlerT = handlerT;
            _listeners = listeners;
        }

        public Task UpdateAllActive(DateTime cursor)
        {
            return _handler.UpdateAllActive(cursor);
        }

        public async Task<bool> Update(T groupEntity, DateTime cursor)
        {
            var result = await _handlerT.Update(groupEntity, cursor);
            foreach (var listener in _listeners)
            {
                await listener.OnUpdate(groupEntity, cursor);
            }

            return result;
        }
    }
}
