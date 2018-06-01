// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageValidatingEntity : IValidatingEntity<Package>
    {
        public PackageValidatingEntity(Package entity)
        {
            EntityRecord = entity;
        }

        public int Key
        {
            get
            {
                return EntityRecord.Key;
            }
        }

        public Package EntityRecord { get; }

        public PackageStatus Status
        {
            get
            {
                return EntityRecord.PackageStatusKey;
            }
        }

        public DateTime Created
        {
            get
            {
                return EntityRecord.Created;
            }
        }
    }
}
