using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;

namespace Validation.Orchestrator
{
    public class ValidationConfigurationItem
    {
        private const string NodeName = "validation";
        private const string NameAttribute = "name";
        private const string FailAfterAttribute = "failAfterMinutes";
        private const string RunAfterNodeName = "runAfter";
        private const string RunAfterValidationAttribute = "validation";

        private static HashSet<string> AllowedNodeAttributes = new HashSet<string>(new[] { NameAttribute, FailAfterAttribute });
        private static HashSet<string> RequiredNodeAttributes = new HashSet<string>(new[] { NameAttribute, FailAfterAttribute });

        public string Name { get; set; }
        public TimeSpan FailAfter { get; set; }
        public List<string> RequiredValidations { get; set; }

        public ValidationConfigurationItem()
        {
            this.Name = "";
            this.FailAfter = TimeSpan.Zero;
            this.RequiredValidations = new List<string>();
        }

        public ValidationConfigurationItem(XmlNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.Name != NodeName)
            {
                throw new ConfigurationErrorsException($"Invalid node name: {node.Name}", node);
            }

            ValidateAttributes(node, RequiredNodeAttributes, AllowedNodeAttributes);

            this.RequiredValidations = new List<string>();

            this.Name = node.Attributes[NameAttribute].Value;
            if (string.IsNullOrWhiteSpace(this.Name))
            {
                throw new ConfigurationErrorsException("Validation name cannot be empty", node);
            }

            var failAfterMinutesValue = node.Attributes[FailAfterAttribute].Value;
            if (int.TryParse(failAfterMinutesValue, out int failAfterMinutes))
            {
                this.FailAfter = TimeSpan.FromMinutes(failAfterMinutes);
            }
            else
            {
                throw new ConfigurationErrorsException($"Failed to parse the {FailAfterAttribute} value: {failAfterMinutesValue}", node);
            }

            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name != RunAfterNodeName)
                {
                    throw new ConfigurationErrorsException($"Unexpected validation child node: {childNode.Name}", node);
                }

                ValidateAttributes(childNode, new[] { RunAfterValidationAttribute }, new[] { RunAfterValidationAttribute });

                var validationPrerequisite = childNode.Attributes[RunAfterValidationAttribute].Value;
                this.RequiredValidations.Add(validationPrerequisite);
            }
        }

        private static void ValidateAttributes(XmlNode node, IEnumerable<string> requiredAttributes, IEnumerable<string> allowedAttributes)
        {
            var requiredAttributesSet = new HashSet<string>(requiredAttributes);
            var allowedAttributesSet = new HashSet<string>(allowedAttributes);

            foreach ( XmlAttribute attribute in node.Attributes )
            {
                if (!allowedAttributesSet.Contains(attribute.Name))
                {
                    throw new ConfigurationErrorsException($"Unknown attribute of the validation node: {attribute.Name}", node);
                }

                requiredAttributesSet.Remove(attribute.Name);
            }

            if (requiredAttributesSet.Any())
            {
                string missingAttributes = string.Join(", ", requiredAttributesSet);

                throw new ConfigurationErrorsException($"Missing required attribute(s) of the validation node: {missingAttributes}", node);
            }
        }
    }
}
