using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace Validation.Orchestrator
{
    public class ConfigurationHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            return GetValidationConfigurationItems(section);
        }

        private const string SectionName = "validations";

        private ICollection<ValidationConfigurationItem> GetValidationConfigurationItems(XmlNode section)
        {
            if (section.Name != SectionName)
            {
                throw new InvalidOperationException($"Invalid configuration section passed: {section.Name}");
            }

            var validations = new List<ValidationConfigurationItem>();

            foreach (XmlNode childNode in section.ChildNodes)
            {
                validations.Add(new ValidationConfigurationItem(childNode));
            }

            return validations;
        }
    }
}
