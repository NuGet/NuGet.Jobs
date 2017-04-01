// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.Validators;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation.Runner
{
    internal static class ApplicationInsightsHelper
    {
        /// <summary>
        /// Tracks orchestration attempts
        /// </summary>
        public static void TrackOrchestration()
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("OrchestrationAttempted");

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }

        /// <summary>
        /// Tracks validator run attempts
        /// </summary>
        /// <param name="validatorName">The name of the validator attempted.</param>
        public static void TrackValidatorRun(string validatorName)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("ValidatorAttempted");
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.ValidatorName, validatorName);

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }

        /// <summary>
        /// Tracks the result of validation. If result is <see cref="ValidationResult.Asynchronous"/> then tracks it in 
        /// a separate event (callback will register another success or failure, so this way it would be easier to match
        /// amounts of attempts and outcomes).
        /// </summary>
        /// <param name="validatorName">The name of the validator</param>
        /// <param name="result">String representation of the outcome</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorResult(string validatorName, string result, string packageId, string packageVersion)
        {
            if (result == ValidationResult.Asynchronous.ToString())
            {
                TrackValidatorAsync(validatorName, packageId, packageVersion);
            }
            else
            {
                Common.ApplicationInsightsHelper.TrackValidatorResult(validatorName, result, packageId, packageVersion);
            }
        }

        /// <summary>
        /// Tracks validator async results (i.e. the case when validator queueud job somewhere and results 
        /// would be processed separately)
        /// </summary>
        /// <param name="validatorName">The name of the validator attempted</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorAsync(string validatorName, string packageId, string packageVersion)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("ValidatorAsync");
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.ValidatorName, validatorName);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageId, packageId);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageVersion, packageVersion);

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }
    }
}