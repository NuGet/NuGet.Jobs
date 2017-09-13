using System;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace Tests.Validation.Orchestrator
{
    public class ConfigurationFacts
    {
        [Fact]
        public void ThrowsIfNull()
        {
            var configurationHandler = new ConfigurationHandler();
            Assert.Throws<ArgumentNullException>(() => configurationHandler.Create(null, null, null));
        }

        [Fact]
        public void ThrowsOnUnknownSection()
        {
            const string section = @"<someSection></someSection>";

            var configurationHandler = new ConfigurationHandler();
            Assert.Throws<InvalidOperationException>(() => configurationHandler.Create(null, null, GetRootNode(section)));
        }

        [Fact]
        public void SmokeTest()
        {
            const string section =
@"
  <validations>
    <validation name=""VCS"" failAfterMinutes=""360"">
      <runAfter validation=""DetonationChamber"" />
     </validation>
   </validations>
 ";

            var configurationHandler = new ConfigurationHandler();
            configurationHandler.Create(null, null, GetRootNode(section));
        }

        [Fact]
        public void ValidationConfigurationItemConstructorThrowsIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ValidationConfigurationItem(null));
        }

        [Fact]
        public void ValidationConfigurationItemConstructorThrowsOnUnexpectedNodeName()
        {
            const string validationXml = "<someRandomNode />";

            Assert.Throws<ConfigurationErrorsException>(() => new ValidationConfigurationItem(GetRootNode(validationXml)));
        }

        [Fact]
        public void ValidationConfigurationItemThrowsOnUnknownAttribute()
        {
            const string validationXml = "<validation someAttribute='someValue' />";
            Assert.Throws<ConfigurationErrorsException>(() => new ValidationConfigurationItem(GetRootNode(validationXml)));
        }

        [Theory]
        [InlineData("<validation />")]
        [InlineData("<validation name='SomeValidation' />")]
        [InlineData("<validation failAfterMinutes='42' />")]
        public void ValidationConfigurationItemThrowsOnMissingAttribute(string validationXml)
        {
            Assert.Throws<ConfigurationErrorsException>(() => new ValidationConfigurationItem(GetRootNode(validationXml)));
        }

        [Theory]
        [InlineData("<validation name='SomeValidation' failAfterMinutes='abc' />")]
        [InlineData("<validation name='' failAfterMinutes='42' />")]
        [InlineData("<validation name='SomeValidation' failAfterMinutes='17.43' />")]
        [InlineData("<validation name='SomeValidation' failAfterMinutes='' />")]
        public void ValidationConfigurationItemThrowsOnInvalidAttributeValue(string validationXml)
        {
            Assert.Throws<ConfigurationErrorsException>(() => new ValidationConfigurationItem(GetRootNode(validationXml)));
        }

        [Fact]
        public void ValidationConfigurationItemSmokeTest()
        {
            const string validationXml = 
@"
<validation name='TestValidation' failAfterMinutes='123'>
  <runAfter validation='Prerequisite1' />
  <runAfter validation='Prerequisite2' />
</validation>
";

            var validation = new ValidationConfigurationItem(GetRootNode(validationXml));

            Assert.Equal("TestValidation", validation.Name);
            Assert.Equal(123, validation.FailAfter.TotalMinutes, 3);
            Assert.Equal(2, validation.RequiredValidations.Count);
            Assert.Contains("Prerequisite1", validation.RequiredValidations);
            Assert.Contains("Prerequisite2", validation.RequiredValidations);
        }

        [Fact]
        public void SerializationTest()
        {
            const string validationXml =
@"
<validation name='TestValidation' failAfterMinutes='123'>
  <runAfter validation='Prerequisite1' />
  <runAfter validation='Prerequisite2' />
</validation>
";

            var vci = new ValidationConfigurationItem(GetRootNode(validationXml));

            var x = new XmlSerializer(typeof(ValidationConfigurationItem));
            string xml;

            using (var sw = new StringWriter())
            using (var xw = XmlWriter.Create(sw))
            {
                x.Serialize(xw, vci);
                xml = sw.ToString();
            }
        }

        private static XmlNode GetRootNode(string xml)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xml);

            return xmlDocument.FirstChild;
        }
    }
}
