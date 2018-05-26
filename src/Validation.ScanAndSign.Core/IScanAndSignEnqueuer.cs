// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation.
        /// </summary>
        Task EnqueueScanAsync(Guid validationId, string nupkgUrl);

        /// <summary>
        /// Enqueues Scan And Sign operation.
        /// </summary>
        /// <param name="request">The requested package validation.</param>
        /// <param name="v3ServiceIndexUrl">The service index URL that should be put on the package's repository signature.</param>
        /// <param name="owners">The list of owners that should be put on the package's repository signature.</param>
        Task EnqueueScanAndSignAsync(IValidationRequest request, string v3ServiceIndexUrl, List<string> owners);
    }
}