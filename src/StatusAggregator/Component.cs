using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator
{
    public abstract class Component : IComponent
    {
        public const char ComponentPathDivider = '/';

        public Component()
        {
        }

        public Component(
            string name,
            string description)
        {
            Name = name;
            Description = description;
            SubComponents = Enumerable.Empty<Component>();
        }

        public Component(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : this(name, description)
        {
            SubComponents = subComponents;
        }

        public string Name { get; set; }

        public string Description { get; set; }

        public IEnumerable<IComponent> SubComponents { get; set; }

        public abstract ComponentStatus Status { get; set; }

        public static string ToRowKeySafeComponentPath(string componentPath)
        {
            return componentPath.Replace(ComponentPathDivider, '_');
        }
    }
}
