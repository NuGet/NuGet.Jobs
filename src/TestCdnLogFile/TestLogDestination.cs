// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace TestCdnLogFile
{
    public class TestLogDestination : ILogDestination
    {
        public Task<AsyncOperationResult> TryWriteAsync(
            Stream inputStream,
            Action<string, Stream, Stream> writeAction,
            string destinationFileName,
            ContentType destinationContentType,
            CancellationToken token)
        {
            writeAction(destinationFileName, inputStream, Stream.Null);
            return Task.FromResult(new AsyncOperationResult(operationResult: true, operationException: null));
        }
    }
}
