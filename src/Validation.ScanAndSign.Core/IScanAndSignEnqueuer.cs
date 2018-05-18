// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation.
        /// </summary>
        /// <param name="request">Request data</param>
        Task EnqueueScanAsync(IValidationRequest request);
    }
}