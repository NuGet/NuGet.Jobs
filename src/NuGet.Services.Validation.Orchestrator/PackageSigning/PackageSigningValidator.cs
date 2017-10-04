// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.PackageSigning
{
    using ValidatorStateService = IValidatorStateService<PackageSigningValidator>;

    public class PackageSigningValidator : IValidator
    {
        private readonly ValidatorStateService _validatorStateService;
        private readonly IPackageSignatureVerifier _packageSignatureVerifier;
        private readonly ILogger<PackageSigningValidator> _logger;

        public PackageSigningValidator(
            ValidatorStateService validatorStateService,
            IPackageSignatureVerifier packageSignatureVerifier,
            ILogger<PackageSigningValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _packageSignatureVerifier = packageSignatureVerifier ?? throw new ArgumentNullException(nameof(packageSignatureVerifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ValidationStatus> GetStatusAsync(IValidationRequest request)
        {
            return Task.FromResult(_validatorStateService.GetStatus(request));
        }

        public async Task<ValidationStatus> StartValidationAsync(IValidationRequest request)
        {
            // Check that this is the first validation for this specific request.
            var currentStatus = await GetStatusAsync(request);

            if (currentStatus != ValidationStatus.NotStarted)
            {
                _logger.LogError(
                    Error.PackageSigningValidationAlreadyStarted,
                    "Package Signing validation with validationId {validationId} ({packageId} {packageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                throw new Exception("TODO: What's the exception if the job has already started?");
            }

            // Kick off the verification process. Note that the jobs will not verify the package until the
            // state of this validator has been persisted to the database.
            await _packageSignatureVerifier.StartVerificationAsync(request);
            await _validatorStateService.AddStatusAsync(request, ValidationStatus.Incomplete);

            return ValidationStatus.Incomplete;
        }
    }
}
