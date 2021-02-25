// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Validation.ContentScan.Core;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class StartContentScanData
    {
        public StartContentScanData(
            Guid validationSetId,
            Uri blobUri)
        {
            ValidationSetId = validationSetId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
        }

        public Guid ValidationSetId { get; }
        public Uri BlobUri { get; }
    }
}
