using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator
{
    public class Component
    {
        public string Name { get; set; }

        public string Description { get; set; }

        private ComponentStatus _status;
        public ComponentStatus Status
        {
            get
            {
                if (!SubComponents.Any())
                {
                    return _status;
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
    }
}
