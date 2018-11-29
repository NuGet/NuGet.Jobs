// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    class OrchestratorContentFileMetadataService : IContentFileMetadataService
    {
        public OrchestratorContentFileMetadataService(IOptionsSnapshot<FlatContainerConfiguration> flatContainerOptionsAccessor)
        {
            if (flatContainerOptionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(flatContainerOptionsAccessor));
            }

            if (flatContainerOptionsAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(flatContainerOptionsAccessor)}.Value property cannot be null", nameof(flatContainerOptionsAccessor));
            }

            PackageContentFolderName = flatContainerOptionsAccessor.Value.ContainerName;
        }

        public string PackageContentFolderName { get; }

        public string PackageContentPathTemplate => CoreConstants.PackageContentFileSavePathTemplate;
    }
}
