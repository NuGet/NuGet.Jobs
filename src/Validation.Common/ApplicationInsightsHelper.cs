// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Common
{
    public static class ApplicationInsightsHelper
    {
        /// <summary>
        /// Tracks validation requests.
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of the validator queued.</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorQueued(this ILogger logger, string validatorName, string packageId, string packageVersion)
        {
            logger.LogInformation($"{{{TraceConstants.EventName}}}: " +
                    $"{{{TraceConstants.ValidatorName}}} " +
                    $"for package {{{TraceConstants.PackageId}}} " +
                    $"v.{{{TraceConstants.PackageVersion}}}",
                "ValidatorQueued", 
                validatorName, 
                packageId, 
                packageVersion);
        }

        /// <summary>
        /// Tracks the result of the validation.
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of validator attempted</param>
        /// <param name="result">Validation result</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        public static void TrackValidatorResult(this ILogger logger, string validatorName, string result, string packageId, string packageVersion)
        {
            logger.LogInformation($"{{{TraceConstants.EventName}}}: " +
                    $"{{{TraceConstants.ValidatorName}}} " +
                    $"for package {{{TraceConstants.PackageId}}} " +
                    $"v.{{{TraceConstants.PackageVersion}}} " +
                    $"resulted in {{Result}}",
                "ValidatorResult",
                validatorName,
                packageId,
                packageVersion,
                result);
        }

        /// <summary>
        /// Tracks the exception occured during validation
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of the validator that was running when exception happened</param>
        /// <param name="ex">The exception to track</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        /// <param name="validationId">Validation ID that was being processed when exception happened</param>
        public static void TrackValidatorException(this ILogger logger, string validatorName, Exception ex, string packageId, string packageVersion, Guid validationId)
        {
            logger.LogError(new EventId(logger.GetHashCode()), ex, 
                    $"{{{TraceConstants.EventName}}} " +
                    $"occurred while running {{{TraceConstants.ValidatorName}}} {{{TraceConstants.ValidationId}}}" +
                    $"on package {{{TraceConstants.PackageId}}}" +
                    $"v. {{{TraceConstants.PackageVersion}}}", 
                "ValidatorException",
                validationId,
                validatorName,
                packageId,
                packageVersion);
        }
    }
}
