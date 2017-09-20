// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Validation.Vcs;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorProvider : IValidatorProvider
    {
        private const string VcsValidatorName = "VcsValidator";

        private readonly Func<VcsValidator> _vcsValidatorFactory;

        public ValidatorProvider(
            Func<VcsValidator> vcsValidatorFactory)
        {
            _vcsValidatorFactory = vcsValidatorFactory ?? throw new ArgumentNullException(nameof(vcsValidatorFactory));
        }

        public IValidator GetValidator(string validationName)
        {
            validationName = validationName ?? throw new ArgumentNullException(nameof(validationName));

            switch (validationName)
            {
                case VcsValidatorName:
                    return _vcsValidatorFactory();
            }

            throw new ArgumentException($"Unknown validation name: {validationName}", nameof(validationName));
        }
    }
}
