// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.Validators;

namespace NuGet.Jobs.Validation.Runner
{
    internal static class ApplicationInsightsHelper
    {
        /// <summary>
        /// Tracks orchestration attempts
        /// </summary>
        /// <param name="logger">Logger to log to</param>
        public static void TrackOrchestration(this ILogger logger)
        {
            logger.LogInformation($"{{{TraceConstants.EventName}}}: " +
                "Another iteration of validator orchestration loop has started");
        }

        /// <summary>
        /// Tracks validator run attempts
        /// </summary>
        /// <param name="logger">Logger to log to</param>
        /// <param name="validatorName">The name of the validator attempted.</param>
        public static void TrackValidatorRun(this ILogger logger, string validatorName)
        {
            logger.LogInformation($"{{{TraceConstants.EventName}}}: " +
                    $"Another iteration of validation loop for {{{TraceConstants.ValidatorName}}} " +
                     "has started",
                "ValidatorAttempted",
                validatorName);
        }

        /// <summary>
        /// Tracks the result of validation. If result is <see cref="ValidationResult.Asynchronous"/> then tracks it in 
        /// a separate event (task finish processor will register another success or failure, so this way it would be 
        /// easier to match amounts of attempts and outcomes).
        /// </summary>
        /// <param name="logger">Logger to log to</param>
        /// <param name="validatorName">The name of the validator</param>
        /// <param name="result">String representation of the outcome</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorResult(this ILogger logger, string validatorName, string result, string packageId, string packageVersion)
        {
            if (result == ValidationResult.Asynchronous.ToString())
            {
                TrackValidatorAsyncJob(logger, validatorName, packageId, packageVersion);
            }
            else
            {
                Common.ApplicationInsightsHelper.TrackValidatorResult(logger, validatorName, result, packageId, packageVersion);
            }
        }

        /// <summary>
        /// Tracks validator async results (i.e. the case when validator queueud job somewhere and results 
        /// would be processed separately)
        /// </summary>
        /// <param name="logger">Logger to log to</param>
        /// <param name="validatorName">The name of the validator attempted</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackValidatorAsyncJob(this ILogger logger, string validatorName, string packageId, string packageVersion)
        {
            logger.LogInformation($"{{{TraceConstants.EventName}}}: " +
                    $"running a {{{TraceConstants.ValidatorName}}} validator " +
                    $"for package {{{TraceConstants.PackageId}}} " +
                    $"v.{{{TraceConstants.PackageVersion}}} resulted in starting async task",
                "ValidatorAsync",
                validatorName,
                packageId,
                packageVersion);
        }

        /// <summary>
        /// Tracks any exeption that leaves the validator uncaught
        /// </summary>
        /// <param name="logger">Logger to log to</param>
        /// <param name="ex">Exception produced</param>
        /// <param name="validatorName">The name of the validator attempted</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        public static void TrackUncaughtValidatorExceptions(this ILogger logger, string validatorName, Exception ex, string packageId, string packageVersion)
        {
            logger.LogError(new EventId(logger.GetHashCode()), ex,
                    $"{{{TraceConstants.EventName}}}: " +
                    $"A {{{TraceConstants.ValidatorName}}} validator " +
                    $"checking the {{{TraceConstants.PackageId}}} " +
                    $"v.{{{TraceConstants.PackageVersion}}} produced exception",
                "UncaughtValidatorException",
                validatorName,
                packageId,
                packageVersion);
        }
    }
}
