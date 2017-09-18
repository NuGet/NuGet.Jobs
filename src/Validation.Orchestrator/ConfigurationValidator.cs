// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.Options;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides a methods for checking configuration validity
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ValidationConfiguration configuration;

        public ConfigurationValidator(IOptions<ValidationConfiguration> optionsAccessor)
        {
            this.configuration = optionsAccessor.Value;
        }

        /// <summary>
        /// Checks if configuration object is valid
        /// </summary>
        public void Validate()
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            CheckValidationsNumber(configuration.Validations);

            CheckPropertyValues(configuration);

            CheckDuplicateValidations(configuration);

            CheckUnknownPrerequisites(configuration);

            CheckPrerequisitesLoops(configuration);
        }

        private static void CheckValidationsNumber(List<ValidationConfigurationItem> validations)
        {
            if (validations == null || !validations.Any())
            {
                throw new ConfigurationErrorsException("Must have at least one validation declared");
            }
        }

        private static void CheckPropertyValues(ValidationConfiguration configuration)
        {
            foreach (var validationConfigurationItem in configuration.Validations)
            {
                if ( string.IsNullOrWhiteSpace(validationConfigurationItem.Name) )
                {
                    throw new ConfigurationErrorsException("Validation name cannot be empty");
                }

                if (validationConfigurationItem.FailAfter == TimeSpan.Zero)
                {
                    throw new ConfigurationErrorsException($"failAfter timeout must be set for validation {validationConfigurationItem.Name}");
                }
            }
        }

        private static void CheckDuplicateValidations(ValidationConfiguration configuration)
        {
            var duplicateValidations = configuration.Validations.Select(v => v.Name).GroupBy(n => n).Where(g => g.Count() > 1).ToList();
            if (duplicateValidations.Any())
            {
                throw new ConfigurationErrorsException($"Duplicate validations: {string.Join(", ", duplicateValidations.Select(d => d.Key))}");
            }
        }

        private static void CheckUnknownPrerequisites(ValidationConfiguration configuration)
        {
            var declaredValidations = new HashSet<string>(configuration.Validations.Select(v => v.Name));
            var prerequisites = new HashSet<string>(configuration.Validations.Select(v => v.RequiredValidations).SelectMany(p => p));
            prerequisites.ExceptWith(declaredValidations);
            if (prerequisites.Any())
            {
                throw new ConfigurationErrorsException($"Unknown validations set as prerequisites: {string.Join(", ", prerequisites)}");
            }
        }

        private static void CheckPrerequisitesLoops(ValidationConfiguration configuration)
        {
            var validations = configuration.Validations.ToDictionary(v => v.Name);

            foreach ( var validationName in validations.Keys )
            {
                CheckPrerequisiteLoop(validations, validationName);
            }
        }

        /// <summary>
        /// Checks if there is a dependency loop that starts from specified validation
        /// </summary>
        /// <remarks>
        /// Implementation details:
        /// Method does the breadth-first search starting from the validation specified as an argument and tracks visited validations.
        /// If at some point the validation to be added to the queue is in the visited list, it means we have circular dependcies.
        /// </remarks>
        /// <param name="validationItems">Map of validation configuration, key is the validation name, value is the configuration item for that validation</param>
        /// <param name="startValidation">The validation name to start searching from</param>
        private static void CheckPrerequisiteLoop(IReadOnlyDictionary<string, ValidationConfigurationItem> validationItems, string startValidation)
        {
            var seenValidations = new HashSet<string>();
            var queue = new Queue<string>(new[] { startValidation });
            while (queue.Any())
            {
                var currentValidationName = queue.Dequeue();
                if (!seenValidations.Add(currentValidationName))
                {
                    // already visited current validation
                    throw new ConfigurationErrorsException($"Prerequisite loop detected starting at {startValidation} and ending at {currentValidationName}.");
                }
                var currentValidation = validationItems[currentValidationName];
                currentValidation.RequiredValidations.ForEach(queue.Enqueue);
            }
        }
    }
}
