using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator
{
    public abstract class Component : IComponent
    {
        public Component()
        {
        }

        public Component(
            string name,
            string description)
        {
            Name = name;
            Description = description;
            SubComponents = Enumerable.Empty<ISubComponent>();
        }

        public Component(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : this(name, description)
        {
            SubComponents = subComponents.Select(s => new SubComponent(s, this));
        }

        public string Name { get; }
        public string Description { get; }
        public abstract ComponentStatus Status { get; set; }
        public IEnumerable<ISubComponent> SubComponents { get; }

        public static string ToRowKeySafeComponentPath(string componentPath)
        {
            return componentPath.Replace(SubComponent.ComponentPathDivider, '_');
        }
    }
}
