// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSigningValidator : IValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IPackageSignatureVerifier _packageSignatureVerifier;
        private readonly ILogger<PackageSigningValidator> _logger;

        public PackageSigningValidator(
            IValidatorStateService validatorStateService,
            IPackageSignatureVerifier packageSignatureVerifier,
            ILogger<PackageSigningValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _packageSignatureVerifier = packageSignatureVerifier ?? throw new ArgumentNullException(nameof(packageSignatureVerifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationStatus> GetStatusAsync(IValidationRequest request)
        {
            var validatorStatus = await _validatorStateService.GetStatusAsync(nameof(PackageSigningValidator), request);

            return validatorStatus.State;
        }

        public async Task<ValidationStatus> StartValidationAsync(IValidationRequest request)
        {
            // Check that this is the first validation for this specific request.
            var validatorStatus = await _validatorStateService.GetStatusAsync(nameof(PackageSigningValidator), request);

            if (validatorStatus.State != ValidationStatus.NotStarted && false)
            {
                _logger.LogWarning(
                    "Package Signing validation with validationId {validationId} ({packageId} {packageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return validatorStatus.State;
            }

            // Kick off the verification process. Note that the jobs will not verify the package until the
            // state of this validator has been persisted to the database.
            validatorStatus.State = ValidationStatus.Incomplete;

            await _packageSignatureVerifier.StartVerificationAsync(request);

            try
            {
                await _validatorStateService.AddStatusAsync(nameof(PackageSigningValidator), validatorStatus);
            }
            catch (DbUpdateException e)
            {
                // This exception happens when the validation ID's state has already been persisted to the database
                // by some other instance of this validator. This may happen if more than one instance of this validator
                // is validating the request, both instances saw no matching status in the database, and then both
                // instances attempted to add a new status to the database. This scenario means that the "StartVerificationAsync"
                // message has been duplicated.
                _logger.LogError(
                    Error.PackageSigningValidationAlreadyStarted,
                    e,
                    "Failed to add validation status for {validationId} ({packageId} {packageVersion}) as a record already exists!",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return await GetStatusAsync(request);
            }

            return ValidationStatus.Incomplete;
        }
    }
}
