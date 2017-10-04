// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorStateService : IValidatorStateService
    {
        private IValidationEntitiesContext _validationContext;

        public ValidatorStateService(IValidationEntitiesContext validationContext)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
        }

        public async Task<ValidatorStatus> GetStatusAsync(string validatorName, IValidationRequest request)
        {
            var status = await _validationContext
                                    .ValidatorStatuses
                                    .Where(s => s.ValidationId == request.ValidationId)
                                    .FirstOrDefaultAsync();

            if (status == null)
            {
                return new ValidatorStatus
                {
                    ValidationId = request.ValidationId,
                    PackageKey = request.PackageKey,
                    ValidatorName = validatorName,
                    State = ValidationStatus.NotStarted,
                };
            }
            else if (status.PackageKey != request.PackageKey)
            {
                throw new ArgumentException(
                    $"Validation expected package key {status.PackageKey}, actual {request.PackageKey}",
                    nameof(request));
            }
            else if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Validation expected validator {status.ValidatorName}, actual {validatorName}",
                    nameof(request));
            }

            return status;
        }

        public Task<bool> IsRevalidationRequestAsync(string validatorName, IValidationRequest request)
        {
            return _validationContext
                        .ValidatorStatuses
                        .Where(s => s.PackageKey == request.PackageKey)
                        .Where(s => s.ValidatorName == validatorName)
                        .Where(s => s.ValidationId != request.ValidationId)
                        .AnyAsync();
        }

        public async Task AddStatusAsync(string validatorName, ValidatorStatus status)
        {
            if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{validatorName}', actual: '{status.ValidatorName}'",
                    nameof(status));
            }

            _validationContext.ValidatorStatuses.Add(status);

            await _validationContext.SaveChangesAsync();
        }

        public async Task SaveStatusAsync(string validatorName, ValidatorStatus status)
        {
            if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{validatorName}', actual: '{status.ValidatorName}'",
                    nameof(status));
            }

            await _validationContext.SaveChangesAsync();
        }
    }
}
