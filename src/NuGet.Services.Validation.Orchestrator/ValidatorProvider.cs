// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorProvider : IValidatorProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ValidatorProvider> _logger;
        private readonly Dictionary<string, Type> _validatorTypes;

        public ValidatorProvider(IServiceProvider serviceProvider, ILogger<ValidatorProvider> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            using (_logger.BeginScope("Enumerating all IValidator implementations"))
            {
                _logger.LogTrace("Before enumeration");
                _validatorTypes = AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => typeof(IValidator).IsAssignableFrom(type) && type != typeof(IValidator))
                    .ToDictionary(type => type.Name);
                _logger.LogTrace("After enumeration, got {NumImplementations} implementations: {TypeNames}", 
                    _validatorTypes.Count, 
                    _validatorTypes.Keys);
            }
        }

        public IValidator GetValidator(string validationName)
        {
            validationName = validationName ?? throw new ArgumentNullException(nameof(validationName));

            if (_validatorTypes.TryGetValue(validationName, out Type validatorType))
            {
                return (IValidator)_serviceProvider.GetRequiredService(validatorType);
            }

            throw new ArgumentException($"Unknown validation name: {validationName}", nameof(validationName));
        }
    }
}
