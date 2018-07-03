using System.Collections.Generic;

namespace StatusAggregator
{
    public interface IComponent
    {
        string Name { get; }
        string Description { get; }
        ComponentStatus Status { get; set; }
        IEnumerable<ISubComponent> SubComponents { get; }
    }
}
