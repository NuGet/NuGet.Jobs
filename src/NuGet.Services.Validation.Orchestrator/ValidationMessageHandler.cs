// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly int _missingPackageRetryCount;
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationSetProvider _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor _validationOutcomeProcessor;
        private readonly ILogger<ValidationMessageHandler> _logger;

        public ValidationMessageHandler(
            int missingPackageRetryCount,
            ICorePackageService galleryPackageService,
            IValidationSetProvider validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor validationOutcomeProcessor,
            ILogger<ValidationMessageHandler> logger)
        {
            if (missingPackageRetryCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(missingPackageRetryCount),
                    "Missing package retry count must be at least 1");
            }

            _missingPackageRetryCount = missingPackageRetryCount;

            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _validationSetProvider = validationSetProvider ?? throw new ArgumentNullException(nameof(validationSetProvider));
            _validationSetProcessor = validationSetProcessor ?? throw new ArgumentNullException(nameof(validationSetProcessor));
            _validationOutcomeProcessor = validationOutcomeProcessor ?? throw new ArgumentNullException(nameof(validationOutcomeProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (_logger.BeginScope("Handling message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                message.PackageId,
                message.PackageVersion,
                message.ValidationTrackingId))
            {
                var package = _galleryPackageService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageVersion);

                if (package == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    if (message.DeliveryCount - 1 >= _missingPackageRetryCount)
                    {
                        _logger.LogWarning("Could not find package {PackageId} {PackageVersion} in DB after {DeliveryCount} tries, dropping message",
                            message.PackageId,
                            message.PackageVersion,
                            message.DeliveryCount);

                        return true;
                    }
                    else
                    {
                        _logger.LogInformation("Could not find package {PackageId} {PackageVersion} in DB, retrying",
                            message.PackageId,
                            message.PackageVersion);

                        return false;
                    }
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message.ValidationTrackingId, package);

                if (validationSet == null)
                {
                    _logger.LogInformation("The validation request for {PackageId} {PackageVersion} validation set {ValidationSetId} is a duplicate. Discarding.",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                await _validationSetProcessor.ProcessValidationsAsync(validationSet, package);
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, package);
            }
            return true;
        }
    }
}
