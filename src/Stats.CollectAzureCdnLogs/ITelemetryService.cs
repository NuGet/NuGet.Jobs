// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.CollectAzureCdnLogs
{
    public interface ITelemetryService
    {
        void TrackRawLogCount(int count);
        void TrackProcessedBlob(string fileName, long compressedBytes, int lineCount);
        IDisposable TrackLogProcessingDuration(string fileName);
    }
}