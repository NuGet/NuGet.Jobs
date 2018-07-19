// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace Validation.Symbols
{
    public interface ITelemetryService : ISubscriptionProcessorTelemetryService
    {
        /// <summary>
        /// Tracks the metric for the packages not being found.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        void TrackPackageNotFoundEvent(string packageId, string packageNormalizedVersion);

        /// <summary>
        /// Tracks the metric for the symbol packages not being found.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        void TrackSymbolNotFoundEvent(string packageId, string packageNormalizedVersion);

        /// <summary>
        /// Tracks the metric for the validation execution time.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="executiontimeInSeconds">The execution time in seconds.</param>
        /// <param name="symbolCount">The count of symbols validated.</param>
        void TrackSymbolValidationTimeEvent(string packageId, string packageNormalizedVersion, long executiontimeInSeconds, int symbolCount);
    }
}
