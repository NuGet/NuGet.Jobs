// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using NuGet.Services.Logging;
using System;

namespace NuGet.Jobs.Validation.Common
{
    public static class ApplicationInsightsHelper
    {
        /// <summary>
        /// Tracks validation requests.
        /// </summary>
        /// <param name="validatorName">The name of the validator queued.</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorQueued(string validatorName, string packageId, string packageVersion)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("ValidatorQueued");
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.ValidatorName, validatorName);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageId, packageId);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageVersion, packageVersion);

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }

        /// <summary>
        /// Tracks the result of the validation.
        /// </summary>
        /// <param name="validatorName">The name of validator attempted</param>
        /// <param name="result">Validation result</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        public static void TrackValidatorResult(string validatorName, string result, string packageId, string packageVersion)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("ValidatorResolution");
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.ValidatorName, validatorName);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageId, packageId);
            eventTelemetry.Properties.Add(ApplicationInsightsConstants.PackageVersion, packageVersion);
            eventTelemetry.Properties.Add("Result", result);

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }

        /// <summary>
        /// Tracks the exception occured during validation
        /// </summary>
        /// <param name="validatorName">The name of the validator that was running when exception happened</param>
        /// <param name="ex">The exception to track</param>
        public static void TrackValidatorException(string validatorName, Exception ex)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var exceptionTelemetry = new ExceptionTelemetry(ex);
            exceptionTelemetry.Properties.Add(ApplicationInsightsConstants.ValidatorName, validatorName);

            telemetryClient.TrackException(exceptionTelemetry);
            telemetryClient.Flush();
        }
    }
}
