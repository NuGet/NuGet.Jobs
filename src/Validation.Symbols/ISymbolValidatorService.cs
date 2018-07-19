// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public interface ISymbolValidatorService
    {
        /// <summary>
        /// Validates the symbols against the PE files. 
        /// </summary>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="token">A cancellation tokne to be used for cancellation of the async execution.</param>
        /// <returns></returns>
        Task<IValidationResult> ValidateSymbolsAsync(string packageId, string packageNormalizedVersion, CancellationToken token);

        /// <summary>
        /// Reads the files from the zip stream.
        /// </summary>
        /// <param name="stream">The zip stream.</param>
        /// <param name="matchingExtensions">A set of matching file extensions to filter the results.</param>
        /// <returns>A hash set of the full file paths inside the zip stream.</returns>
        HashSet<string> ReadFilesFromZipStream(Stream stream, params string[] matchingExtensions);

        /// <summary>
        /// Extracts the files from the zip stream.
        /// </summary>
        /// <param name="stream">The zip stream.</param>
        /// <param name="targetDirectory">The target diorectoryu where to extract.</param>
        /// <param name="symbolFilter">A collection of full symbol files name to be used for filtering.</param>
        /// <returns>The hasset of the full paths for the extracted files.</returns>
        HashSet<string> ExtractFilesFromZipStream(Stream stream, string targetDirectory, HashSet<string> symbolFilter = null);
    }
}
