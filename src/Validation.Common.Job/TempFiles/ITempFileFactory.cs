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
        ITempFile CreateTempFile();

        /// <summary>
        /// Opens existing file for reading and makes sure it is deleted on closing.
        /// </summary>
        ITempReadOnlyFile OpenFileForReadAndDelete(string fileName);
    }
}
