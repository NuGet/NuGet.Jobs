// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace Tests.Validation.Orchestrator
{
    public class ConfigurationFacts
    {
        [Fact]
        public void ConfigurationValidatorSmokeTest()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    }
                }
            };

            ConfigurationValidator.Validate(configuration);
        }

        [Fact]
        public void ConfigurationValidatorDetectsDuplicates()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1"
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1"
                    }
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void ConfigurationValidatorDetectsUnknownValidationPrerequisites()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        RequiredValidations = new List<string>{ "SomeValidation" }
                    },
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void ConfigurationValidatorDetectsLoops()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        RequiredValidations = new List<string>{ "Validation1" }
                    }
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void ConfigurationValidatorDetectsSelfReferencingValidation()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        RequiredValidations = new List<string>{ "Validation1" }
                    },
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void ConfigurationValidatorDetectsSelfReferencingValidation2()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        RequiredValidations = new List<string>{ "Validation2" }
                    }
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void ValidationNamesCantBeEmpty()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "",
                        FailAfter = TimeSpan.FromHours(1)
                    }
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void FailureTimeoutsCantBeZero()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "SomeValidation",
                        FailAfter = TimeSpan.Zero
                    }
                }
            };

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationValidator.Validate(configuration));
        }
    }
}
