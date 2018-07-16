// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Error = NuGet.Services.Validation.Orchestrator.Error;

namespace NuGet.Services.Validation.Symbols
{
    [ValidatorName(ValidatorName.SymbolValidator)]
    public class SymbolValidator : BaseValidator, IValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly ISymbolMessageEnqueuer _symbolMessageEnqueuer;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolValidator> _logger;

        public SymbolValidator(
            IValidatorStateService validatorStateService,
            ISymbolMessageEnqueuer symbolMessageEnqueuer,
            ITelemetryService telemetryService,
            ILogger<SymbolValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _symbolMessageEnqueuer = symbolMessageEnqueuer ?? throw new ArgumentNullException(nameof(symbolMessageEnqueuer));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);
            var result = validatorStatus.ToValidationResult();
            if (validatorStatus.State == ValidationStatus.Failed)
            {
                _logger.LogInformation(
                           "SymbolValidationFailure " +
                           "status = {ValidationStatus}, snupkg URL = {NupkgUrl}, validation issues = {Issues}",
                           result.Status,
                           result.NupkgUrl,
                           result.Issues.Select(i => i.IssueCode));
            }
            return validatorStatus.ToValidationResult();
        }

        /// <summary>
        /// The pattern used for the StartAsync:
        /// 1. Check if a validation was already started
        /// 2. Only if a validation was not started queue the message to be processed.
        /// 3. After the message is queued, update the ValidatorStatus for the <paramref name="request"/>.
        /// </summary>
        /// <param name="request">The request to be send to the validator job queue.</param>
        /// <returns>The validation status.</returns>
        public async Task<IValidationResult> StartAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Symbol validation for {0} has already started.",
                    request.NupkgUrl);

                return validatorStatus.ToValidationResult();
            }

            // Due to race conditions or failure of method TryAddValidatorStatusAsync the same message can be enqueued multiple times
            // Log this information to postmortem evaluate this behavior
            await _symbolMessageEnqueuer.EnqueueSymbolMessageAsync(request);
            _telemetryService.TrackValidatorEnquedMessage(ValidatorName.SymbolValidator, request.ValidationId, DateTime.UtcNow);

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return result.ToValidationResult();
        }
    }
}
