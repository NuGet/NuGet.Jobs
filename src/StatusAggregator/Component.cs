using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator
{
    public class Component
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
            ComponentStatus status)
            : this(name, description)
        {
            Status = status;
        }

        public Component(
            string name,
            string description,
            IEnumerable<Component> subComponents)
            : this(name, description)
        {
            SubComponents = subComponents;
        }

        public Component(
            string name,
            string description,
            ComponentStatus status,
            IEnumerable<Component> subComponents)
            : this(name, description, status)
        {
            SubComponents = subComponents;
        }

        public string Name { get; set; }

        public string Description { get; set; }

        private ComponentStatus? _status = null;
        public ComponentStatus Status
        {
            get
            {
                if (_status.HasValue)
                {
                    return _status.Value;
                }

                if (!SubComponents.Any())
                {
                    return ComponentStatus.Up;
                }

                // If all subcomponents are up, we are up.
                if (SubComponents.All(c => c.Status == ComponentStatus.Up))
                {
                    return ComponentStatus.Up;
                }

                // If all subcomponents are down, we are down.
                if (SubComponents.All(c => c.Status == ComponentStatus.Down))
                {
                    return ComponentStatus.Down;
                }

                // Otherwise, we are degraded, because some subcomponents are degraded or down but not all.
                return ComponentStatus.Degraded;
            }
            set
            {
                _status = value;
            }
        }

        public IEnumerable<Component> SubComponents { get; set; }

        public static string ToRowKeySafeComponentPath(string componentPath)
        {
            return componentPath.Replace(ComponentPathDivider, '_');
        }
    }
}
