using System;
using System.Threading.Tasks;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public interface IEventUpdater
    {
        Task<bool> UpdateEvent(EventEntity eventEntity, DateTime nextCreationTime);
    }
}
