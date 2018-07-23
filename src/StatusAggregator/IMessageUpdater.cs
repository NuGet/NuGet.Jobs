using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public interface IMessageUpdater
    {
        Task CreateMessageForEventStart(EventEntity eventEntity, DateTime nextCreationTime);

        Task CreateMessageForEventEnd(EventEntity eventEntity);
    }
}
