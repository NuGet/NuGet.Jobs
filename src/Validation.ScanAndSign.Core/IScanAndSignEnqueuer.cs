// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan.</param>
        /// <param name="scheduledEnqueueTimeOverride">If specified, the value will override the default the scheduled message enqueue time.</param>
        Task EnqueueScanAsync(Guid validationId, string nupkgUrl, DateTimeOffset? scheduledEnqueueTimeOverride = null);

        /// <summary>
        /// Enqueues Scan And Sign operation.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan and sign is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan and sign.</param>
        /// <param name="v3ServiceIndexUrl">The service index URL that should be put on the package's repository signature.</param>
        /// <param name="owners">The list of owners that should be put on the package's repository signature.</param>
        /// <param name="scheduledEnqueueTimeOverride">If specified, the value will override the default the scheduled message enqueue time.</param>
        Task EnqueueScanAndSignAsync(Guid validationId, string nupkgUrl, string v3ServiceIndexUrl, IReadOnlyList<string> owners, DateTimeOffset? scheduledEnqueueTimeOverride = null);
    }
}