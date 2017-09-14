// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides a methods for checking configuration validity
    /// </summary>
    public static class ConfigurationValidator
    {
        /// <summary>
        /// Checks if configuration object is valid
        /// </summary>
        /// <param name="configuration">Configuration object to check</param>
        public static void Validate(Configuration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            CheckDuplicateValidations(configuration);

            CheckUnknownPrerequisites(configuration);

            CheckPrerequisitesLoops(configuration);
        }

        private static void CheckPrerequisitesLoops(Configuration configuration)
        {
            var validations = configuration.Validations.ToDictionary(v => v.Name);

            foreach ( var validationName in validations.Keys )
            {
                CheckPrerequisiteLoop(validations, validationName);
            }
        }

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
                    throw new ConfigurationErrorsException($"Prerequisite loop detected starting at {startValidation}");
                }
                var currentValidation = validationItems[currentValidationName];
                currentValidation.RequiredValidations.ForEach(queue.Enqueue);
            }
        }

        private static void CheckUnknownPrerequisites(Configuration configuration)
        {
            var declaredValidations = new HashSet<string>(configuration.Validations.Select(v => v.Name));
            var prerequisites = new HashSet<string>(configuration.Validations.Select(v => v.RequiredValidations).SelectMany(p => p));
            prerequisites.ExceptWith(declaredValidations);
            if (prerequisites.Any())
            {
                throw new ConfigurationErrorsException($"Unknown validations set as prerequisites: {string.Join(", ", prerequisites)}");
            }
        }

        private static void CheckDuplicateValidations(Configuration configuration)
        {
            var duplicateValidations = configuration.Validations.Select(v => v.Name).GroupBy(n => n).Where(g => g.Count() > 1).ToList();
            if (duplicateValidations.Any())
            {
                throw new ConfigurationErrorsException($"Duplicate validations: {string.Join(", ", duplicateValidations)}");
            }
        }
    }
}
