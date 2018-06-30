using System.Collections.Generic;

namespace StatusAggregator
{
    public interface IComponent
    {
        string Name { get; set; }
        string Description { get; set; }
        ComponentStatus Status { get; set; }
        IEnumerable<IComponent> SubComponents { get; set; }
    }
}
