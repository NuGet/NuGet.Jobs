// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStreamMetadataAndCopiedStatusWrapper
    {
        public PackageStreamMetadata PackageStreamMetadata { get; }

        public bool Copied { get; }

        public PackageStreamMetadataAndCopiedStatusWrapper(PackageStreamMetadata packageStreamMetadata, bool copied)
        {
            PackageStreamMetadata = packageStreamMetadata;
            Copied = copied;
        }
    }
}