// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorStateService<TValidator> : IValidatorStateService<TValidator>
        where TValidator : IValidator
    {
        private IValidationEntitiesContext _validationContext;
        private string _validatorName;

        public ValidatorStateService(IValidationEntitiesContext validationContext)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorName = typeof(TValidator).Name;
        }

        public ValidationStatus GetStatus(IValidationRequest request)
        {
            var status = _validationContext
                .ValidatorStatuses
                .Where(s => s.ValidationId == request.ValidationId)
                .FirstOrDefault();

            return status?.State ?? ValidationStatus.NotStarted;
        }

        public bool IsRevalidationRequest(IValidationRequest request)
        {
            return _validationContext
                        .ValidatorStatuses
                        .Where(s => s.PackageKey == request.PackageKey)
                        .Where(s => s.ValidatorName == _validatorName)
                        .Where(s => s.ValidationId != request.ValidationId)
                        .Any();
        }

        public async Task AddStatusAsync(IValidationRequest request, ValidationStatus status)
        {
            _validationContext.ValidatorStatuses.Add(new ValidatorStatus
            {
                ValidationId = request.ValidationId,
                PackageKey = request.PackageKey,
                ValidatorName = _validatorName,
                State = status,
            });

            await _validationContext.SaveChangesAsync();
        }

        public async Task SaveStatusAsync(IValidationRequest request, ValidationStatus status)
        {
            var entity = _validationContext
                            .ValidatorStatuses
                            .Where(s => s.ValidationId == request.ValidationId)
                            .First();

            entity.State = status;

            await _validationContext.SaveChangesAsync();
        }
    }
}
