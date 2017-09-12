using System;
using System.Collections.Generic;
using System.Xml;

namespace Validation.Orchestrator
{
    public class ValidationConfigurationItem
    {
        public string Name { get; }
        public TimeSpan FailAfter { get; }
        public ICollection<string> RequiredValidations { get; }

        public ValidationConfigurationItem(XmlNode node)
        {
            throw new NotImplementedException();
        }
    }
}
