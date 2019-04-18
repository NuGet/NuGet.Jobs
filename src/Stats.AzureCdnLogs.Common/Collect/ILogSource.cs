// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public interface ILogSource
    {
        Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token, string prefix = null);

        Task<bool> CleanAsync(Uri fileUri, bool onError, CancellationToken token);

        Task<Stream> OpenReadAsync(Uri fileUri, ContentType contentType, CancellationToken token);

        /// <summary>
        /// Take lock.
        /// </summary>
        /// <param name="fileUri">The file uri.</param>
        /// <param name="token">The token for cancellation.</param>
        /// <returns>The result of the lock action. </returns>
        Task<AzureBlobLockResult> TakeLockAsync(Uri fileUri, CancellationToken token);

        Task<bool> ReleaseLockAsync(Uri fileUri, CancellationToken token);
    }
}
