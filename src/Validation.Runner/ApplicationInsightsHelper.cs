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
            logger.LogInformation($"{{{TraceConstant.EventName}}}: " +
                    $"running a {{{TraceConstant.ValidatorName}}} validator " +
                    $"for package {{{TraceConstant.PackageId}}} " +
                    $"v.{{{TraceConstant.PackageVersion}}} resulted in starting async task",
                "ValidatorAsync",
                validatorName,
                packageId,
                packageVersion);
        }
    }
}
