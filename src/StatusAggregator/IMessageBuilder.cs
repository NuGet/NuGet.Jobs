using System;
using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IMessageBuilder
    {
        Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);

        Task<MessageEntity> CreateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status);

        Task UpdateMessage(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);

        Task DeleteMessage(EventEntity eventEntity, DateTime time);
    }
}
