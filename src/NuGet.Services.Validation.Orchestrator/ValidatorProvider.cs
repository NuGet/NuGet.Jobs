// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                IEnumerable<Type> candidateTypes = GetCandidateTypes(Assembly.GetCallingAssembly());

                _validatorTypes = candidateTypes
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

        private static IEnumerable<Type> GetCandidateTypes(Assembly callingAssembly)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            IEnumerable<Type> candidateTypes = executingAssembly.GetTypes();
            if (callingAssembly != executingAssembly)
            {
                candidateTypes = candidateTypes.Concat(callingAssembly.GetTypes());
            }

            return candidateTypes;
        }
    }
}
