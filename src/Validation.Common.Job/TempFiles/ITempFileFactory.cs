// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Provides temp files for use 
    /// </summary>
    public interface ITempFileFactory
    {
        /// <summary>
        /// Creates empty temp file, returns object that contains path to it and controls its lifetime.
        /// </summary>
        ITempFile CreateTempFile(string directoryName);

        /// <summary>
        /// Creates temp file with specified text, returns object that contains path to it and controls its lifetime.
        /// </summary>
        /// <param name="contents">The contents of the file to be created.</param>
        ITempFile CreateTempFile(string directoryName, string contents);

        /// <summary>
        /// Opens existing file for reading and makes sure it is deleted on closing.
        /// </summary>
        ITempReadOnlyFile OpenFileForReadAndDelete(string fileName);
    }
}
