// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace TestCdnLogFile
{
    public class TestLogSource : ILogSource
    {
        private readonly string _fileToTest;
        private readonly Uri _fakeBlobUri;

        public TestLogSource(string fileToTest)
        {
            _fileToTest = fileToTest ?? throw new ArgumentNullException(nameof(fileToTest));
            _fakeBlobUri = new Uri("https://example/" + Uri.EscapeUriString(Path.GetFileName(fileToTest)));
        }

        public Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token, string prefix = null)
        {
            return Task.FromResult<IEnumerable<Uri>>(new[] { _fakeBlobUri });
        }

        public Task<Stream> OpenReadAsync(Uri blobUri, ContentType contentType, CancellationToken token)
        {
            Stream output = File.OpenRead(_fileToTest);
            if (contentType == ContentType.GZip)
            {
                output = new GZipInputStream(output);
            }

            return Task.FromResult(output);
        }

        public Task<AzureBlobLockResult> TakeLockAsync(Uri blobUri, CancellationToken token)
        {
            return Task.FromResult(new AzureBlobLockResult(
                new CloudBlockBlob(_fakeBlobUri),
                lockIsTaken: true,
                leaseId: "fake-lease",
                linkToken: CancellationToken.None));
        }

        public Task<AsyncOperationResult> TryCleanAsync(AzureBlobLockResult blobLock, bool onError, CancellationToken token)
        {
            return Task.FromResult(new AsyncOperationResult(operationResult: true, operationException: null));
        }

        public Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult blobLock, CancellationToken token)
        {
            return Task.FromResult(new AsyncOperationResult(operationResult: true, operationException: null));
        }
    }
}
