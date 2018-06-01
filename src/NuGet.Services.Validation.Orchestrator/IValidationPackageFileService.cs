﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IValidationFileService
    {
        /// <summary>
        /// The action to delete the package file in the validation container.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be deleted.</param>
        /// <returns></returns>
        Task DeleteValidationPackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// The action to delete the package file.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be deleted.</param>
        /// <returns></returns>
        Task DeletePackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// It will return true if the package associated with this validation set exists.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be found.</param>
        /// <returns></returns>
        Task<bool> DoesPackageFileExistAsync(PackageValidationSet validationSet);

        /// <summary>
        /// It will return true if the package in the validation container associated with this validation set exists.
        /// </summary>
        /// <param name="validationSet"></param>
        /// <returns></returns>
        Task<bool> DoesValidationPackageFileExistAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Download the package content from the packages container to a temporary location on disk.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <returns>The package stream.</returns>
        Task<Stream> DownloadPackageFileToDiskAsync(PackageValidationSet package);

        /// <summary>
        /// Backs up the package file from the location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        Task BackupPackageFileFromValidationSetPackageAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package from the validation container to a location specific for the validation set. This allows the
        /// validation set to have its own copy of the package to mutate (via <see cref="IProcessor"/>) and validate.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package from the packages container to a location specific for the validation set. This allows the
        /// validation set to have its own copy of the package to mutate (via <see cref="IProcessor"/>) and validate.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <returns>The etag of the source package.</returns>
        Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package from a location specific for the validation set to the packages container.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <param name="destAccessCondition">
        /// The access condition used for asserting the state of the destination file.
        /// </param>
        Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition);

        /// <summary>
        /// Copy a package from the validation container to the packages container.
        /// </summary>
        /// <param name="validationSet">The validation set.</param>
        Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package URL to a location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set.</param>
        /// <param name="srcPackageUrl">The source package URL.</param>
        /// <returns></returns>
        Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl);

        /// <summary>
        /// Delete a package from a location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Generates the URI for the specified validating package, which can be used to download it.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <param name="endOfAccess">The timestamp that limits the URI usage period.</param>
        /// <returns>Time limited (if implementation supports) URI for the package.</returns>
        Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, DateTimeOffset endOfAccess);

        /// <summary>
        /// Checks whether the validation set's package file exists.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet);
    }
}