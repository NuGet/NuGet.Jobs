// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// All the metadata needed for the files manipulation in the validation pipeline - save, delete, copy.
    /// </summary>
    public class SymbolValidationFileServiceMetadata : IValidationFileServiceMetadata
    {
        public string FilePublicFolderName => throw new NotImplementedException();

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => throw new NotImplementedException();

        public string ValidationFolderName => throw new NotImplementedException();

        public string FileBackupsFolderName => throw new NotImplementedException();

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
