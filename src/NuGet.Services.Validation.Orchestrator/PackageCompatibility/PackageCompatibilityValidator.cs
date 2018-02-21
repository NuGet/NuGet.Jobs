// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using Validation.PackageCompatibility.Core.Messages;
using Validation.PackageCompatibility.Core.Storage;

namespace NuGet.Services.Validation.PackageCompatibility
{
    /// <summary>
    /// Configuration for initializing the <see cref="PackageCompatibilityValidator"/>.
    /// </summary>
    public class PackageCompatibilityValidator : IValidator
    {
        private IValidatorStateService _validatorStateService;
        private IPackageCompatibilityService _packageCompatibilityService;
        private readonly ILogger<PackageCompatibilityValidator> _logger;

        private IPackageDownloader _packageDownloader;

        public PackageCompatibilityValidator(
            IValidatorStateService validatorStateService,
            IPackageCompatibilityService packageCompatibilityService,
            IPackageDownloader packageDownloader,
            ILogger<PackageCompatibilityValidator> logger)
        {
            _validatorStateService = validatorStateService;
            _packageCompatibilityService = packageCompatibilityService;
            _packageDownloader = packageDownloader;
            _logger = logger;
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            return validatorStatus.ToValidationResult();
        }

        public async Task<IValidationResult> StartValidationAsync(IValidationRequest request)
        {
            // Try check the whole thing
            try
            {
                var validatorStatus = await _validatorStateService.GetStatusAsync(request);

                if (validatorStatus.State != ValidationStatus.NotStarted)
                {
                    _logger.LogWarning(
                          "Package Compatibility validation with validationId {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                          request.ValidationId,
                          request.PackageId,
                          request.PackageVersion);

                    return validatorStatus.ToValidationResult();
                }

                // Add the status, so subsequent calls don't try to reevaluate the same thing
                await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

                var message = new PackageCompatibilityValidationMessage(
                    request.PackageId,
                    request.PackageVersion,
                    new Uri(request.NupkgUrl),
                    request.ValidationId
                    );

                // Do validation
                await Validate(message, CancellationToken.None);

                validatorStatus.State = ValidationStatus.Succeeded;

                await _validatorStateService.SaveStatusAsync(validatorStatus);
                return validatorStatus.ToValidationResult();
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Validation failed for the validation request {0}.{Environment.NewLine}Exception: {1}", request.ToString(), e.ToString());
            }
            return null;
        }

        private async Task Validate(PackageCompatibilityValidationMessage message, CancellationToken cancellationToken)
        {
            using (var packageStream = await _packageDownloader.DownloadAsync(message.NupkgUri, cancellationToken))
            using (var package = new Packaging.PackageArchiveReader(packageStream))
            {
                var warnings = new List<PackLogMessage>();

                foreach (var rule in Packaging.Rules.DefaultPackageRuleSet.Rules)
                {
                    warnings.AddRange(rule.Validate(package));
                }

                await _packageCompatibilityService.SetPackageCompatibilityState(message.ValidationId, warnings);
            }
        }
    }
}
