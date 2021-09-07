// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.ContentScan;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Issues;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Error = NuGet.Services.Validation.Orchestrator.Error;

namespace NuGet.Services.Validation.Orchestrator.ContentScan
{
    [ValidatorName(ValidatorName.NuGetContentScanValidator)]
    public class NuGetContentScanValidator : BaseNuGetValidator, INuGetValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IContentScanEnqueuer _contentScanEnqueuer;
        private readonly ContentScanConfiguration _configuration;
        private readonly ILogger<ContentScanValidator> _logger;

        public NuGetContentScanValidator(
            IValidatorStateService validatorStateService,
            IContentScanEnqueuer contentScanEnqueuer,
            IOptionsSnapshot<ContentScanConfiguration> configurationAccessor,
            ILogger<ContentScanValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _contentScanEnqueuer = contentScanEnqueuer ?? throw new ArgumentNullException(nameof(contentScanEnqueuer));

            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));

            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }

            _configuration = configurationAccessor.Value;
        }

        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await GetValidatorStatusAsync(request);

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Content scan validation with validation Id {ValidationStepId} has already started.",
                    request.ValidationId);

                return GetValidationResponse(validatorStatus);
            }

            await _contentScanEnqueuer.EnqueueContentScanAsync(request.ValidationId, new Uri(request.NupkgUrl));

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return GetValidationResponse(result);
        }

        public async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = await GetValidatorStatusAsync(request);

            return GetValidationResponse(result);
        }

        private async Task<ValidatorStatus> GetValidatorStatusAsync(INuGetValidationRequest request)
        {
            return await _validatorStateService.GetStatusAsync(request);
        }

        private INuGetValidationResponse GetValidationResponse(ValidatorStatus status)
        {
            if (status.State == ValidationStatus.Failed)
            {
                return new NuGetValidationResponse(status.State, status.ValidatorIssues.Select(i => new NoDataValidationIssue(i.IssueCode)).ToList());
            }

            return new NuGetValidationResponse(status.State);
        }
    }
}