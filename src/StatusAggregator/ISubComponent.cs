using Newtonsoft.Json;

namespace StatusAggregator
{
    public interface ISubComponent : IComponent
    {
        [JsonIgnore]
        IComponent Parent { get; }
        string Path { get; }
    }
}
