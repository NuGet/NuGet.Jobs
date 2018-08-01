﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// The validator that ensures the package's repository signature is valid. This does the
    /// final signature validation after a package has been repository signed.
    /// </summary>
    [ValidatorName(ValidatorName.PackageSignatureValidator)]
    public class PackageSignatureValidator : BaseSignatureProcessor, IValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IProcessSignatureEnqueuer _signatureVerificationEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ICorePackageService _packages;
        private readonly ScanAndSignConfiguration _config;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageSignatureValidator> _logger;

        public PackageSignatureValidator(
            IValidatorStateService validatorStateService,
            IProcessSignatureEnqueuer signatureVerificationEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            ICorePackageService packages,
            IOptionsSnapshot<ScanAndSignConfiguration> configAccessor,
            ITelemetryService telemetryService,
            ILogger<PackageSignatureValidator> logger)
          : base(validatorStateService, signatureVerificationEnqueuer, blobProvider, telemetryService, logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configAccessor?.Value == null)
            {
                throw new ArgumentException($"{nameof(ScanAndSignConfiguration)} is required", nameof(configAccessor));
            }

            _config = configAccessor.Value;
        }

        /// <summary>
        /// This validator runs after packages are repository signed. It will verify that the repository
        /// signature is acceptable.
        /// </summary>
        protected override bool RequiresRepositorySignature => true;

        public override async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            var result = await base.GetResultAsync(request);

            return Validate(request, result);
        }

        public override async Task<IValidationResult> StartAsync(IValidationRequest request)
        {
            var result = await base.StartAsync(request);

            return Validate(request, result);
        }

        private IValidationResult Validate(IValidationRequest request, IValidationResult result)
        {
            /// The package signature validator runs after the <see cref="PackageSignatureProcessor" />.
            /// All signature validation issues should be caught and handled by the processor.
            if (result.Status == ValidationStatus.Failed || result.NupkgUrl != null)
            {
                if (!_config.RepositorySigningEnabled)
                {
                    _logger.LogInformation(
                        "Ignoring invalid validation result in package signature validator as repository signing is disabled. " +
                        "Status = {ValidationStatus}, Nupkg URL = {NupkgUrl}, validation issues = {Issues}",
                        result.Status,
                        result.NupkgUrl,
                        result.Issues.Select(i => i.IssueCode));

                    return ValidationResult.Succeeded;
                }

                // TODO: Remove this.
                // See: https://github.com/NuGet/Engineering/issues/1592
                if (HasOwnerWithInvalidUsername(request))
                {
                    _logger.LogWarning(
                        "Ignoring invalid validation result in package signature validator as the package has an owner with an invalid username. " +
                        "Status = {ValidationStatus}, Nupkg URL = {NupkgUrl}, validation issues = {Issues}",
                        result.Status,
                        result.NupkgUrl,
                        result.Issues.Select(i => i.IssueCode));

                    return ValidationResult.Succeeded;
                }

                _logger.LogCritical(
                    "Unexpected validation result in package signature validator. This may be caused by an invalid repository " +
                    "signature. Throwing an exception to force this validation to dead-letter. " +
                    "Status = {ValidationStatus}, Nupkg URL = {NupkgUrl}, validation issues = {Issues}",
                    result.Status,
                    result.NupkgUrl,
                    result.Issues.Select(i => i.IssueCode));

                throw new InvalidOperationException("Package signature validator has an unexpected validation result");
            }

            /// Suppress all validation issues. The <see cref="PackageSignatureProcessor"/> should
            /// have already reported any issues related to the author signature. Customers should
            /// not be notified of validation issues due to the repository signature.
            if (result.Issues.Count != 0)
            {
                _logger.LogWarning(
                    "Ignoring {ValidationIssueCount} validation issues from result. Issues: {Issues}",
                    result.Issues.Count,
                    result.Issues.Select(i => i.IssueCode));

                return new ValidationResult(result.Status);
            }

            return result;
        }

        private bool HasOwnerWithInvalidUsername(IValidationRequest request)
        {
            var registration = _packages.FindPackageRegistrationById(request.PackageId);

            if (registration == null)
            {
                _logger.LogError("Attempted to validate package that has no package registration");

                throw new InvalidOperationException($"Registration for package id {request.PackageId} does not exist");
            }

            var owners = registration.Owners.Select(o => o.Username).ToList();

            if (owners.Any(UsernameHelper.IsInvalid))
            {
                _logger.LogWarning(
                    "Package {PackageId} {PackageVersion} has an owner with an invalid username. {Owners}",
                    request.PackageId,
                    request.PackageVersion,
                    owners);

                return true;
            }

            return false;
        }
    }
}