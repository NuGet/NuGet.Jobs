using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Validation.Orchestrator;
using Xunit;

namespace Tests.Validation.Orchestrator
{
    public class ConfigurationFacts
    {
        [Fact]
        public void SmokeTest()
        {
            const string section =
@"
  <validations>
    <validation name=""VCS"" failAfterMinutes=""360"">
      <runAfter validation = ""DetonationChamber"" />
     </validation>
   </validations>
 ";

            var configurationHandler = new ConfigurationHandler();
            configurationHandler.Create(null, null, GetRootNode(section));
        }

        private static XmlNode GetRootNode(string xml)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xml);

            return xmlDocument;
        }
    }
}
