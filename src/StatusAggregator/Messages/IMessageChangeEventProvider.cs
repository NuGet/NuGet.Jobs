using System.Collections.Generic;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IMessageChangeEventProvider
    {
        /// <summary>
        /// Returns the <see cref="MessageChangeEvent"/>s associated with <see cref="EventEntity"/>.
        /// </summary>
        IEnumerable<MessageChangeEvent> Get(EventEntity eventEntity);
    }
}
