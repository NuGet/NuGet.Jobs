// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The EntityService for the <see cref="NuGetGallery.SymbolPackage"/>.
    /// </summary>
    public class SymbolEntityService : IEntityService<SymbolPackage>
    {
        public SymbolEntityService(ICorePackageService galleryEntityService)
        {
        }

        public IValidatingEntity<SymbolPackage> FindPackageByIdAndVersionStrict(string id, string version)
        {
            throw new NotImplementedException();
        }

        public IValidatingEntity<SymbolPackage> FindByKey(int key)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateStatusAsync(SymbolPackage entity, PackageStatus newStatus, bool commitChanges = true)
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }

        public async Task UpdateMetadataAsync(SymbolPackage entity, object metadata, bool commitChanges = true)
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }

        public List<string> GetOwners(SymbolPackage Entity)
        {
            throw new NotImplementedException();
        }
    }
}
