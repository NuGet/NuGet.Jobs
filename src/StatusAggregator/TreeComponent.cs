using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator
{
    public class TreeComponent : Component
    {
        public TreeComponent()
        {
        }

        public TreeComponent(
            string name,
            string description)
            : base(name, description)
        {
        }

        public TreeComponent(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : base(name, description, subComponents)
        {
        }

        public TreeComponent(
            string name,
            string description,
            ComponentStatus status)
            : this(name, description)
        {
            _status = status;
        }

        public TreeComponent(
            string name,
            string description,
            ComponentStatus status,
            IEnumerable<IComponent> subComponents)
            : this(name, description, subComponents)
        {
            _status = status;
        }

        private ComponentStatus? _status = null;
        public override ComponentStatus Status
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
    }
}
