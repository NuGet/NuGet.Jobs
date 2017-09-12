using System;
using System.Configuration;
using System.Xml;

namespace Validation.Orchestrator
{
    public class ConfigurationHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            throw new NotImplementedException();
        }
    }
}
