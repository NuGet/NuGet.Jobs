using System.Collections.Generic;

namespace StatusAggregator
{
    public class SubComponent : ISubComponent
    {
        public const char ComponentPathDivider = '/';

        private readonly IComponent _component;

        public string Name => _component.Name;
        public string Description => _component.Description;
        public ComponentStatus Status { get => _component.Status; set => _component.Status = value; }
        public IEnumerable<ISubComponent> SubComponents => _component.SubComponents;
        public IComponent Parent { get; }
        public string Path { get; }

        public SubComponent(IComponent component)
        {
            _component = component;
            Parent = null;
            Path = (_component as SubComponent)?.Path ?? _component.Name;
        }

        public SubComponent(IComponent component, IComponent parent)
            : this(component)
        {
            Parent = parent;
            Path = parent.Name + ComponentPathDivider + Path;
        }
    }
}
