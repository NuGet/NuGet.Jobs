// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.GitHubIndexer
{
    public class CheckedOutFile : ICheckedOutFile
    {
        public CheckedOutFile(string filePath, string repoId)
        {
            Path = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RepoId = repoId ?? throw new ArgumentNullException(nameof(repoId));
        }

        public string Path { get; }
        public string RepoId { get; }

        /// <summary>
        /// Opens a FileStream to the checkedout file
        /// </summary>
        /// 
        /// <exception cref="ArgumentException">
        ///    Thrown when path is an empty string (""), contains only white space, or contains one or more
        ///    invalid characters. -or-path refers to a non-file device, such as "con:", "com1:",
        ///    "lpt1:", etc. in an NTFS environment.
        /// </exception>
        /// <exception cref="NotSupportedException:">
        ///     Thrown when path refers to a non-file device, such as "con:", "com1:", "lpt1:", etc. in a
        ///     non-NTFS environment.
        /// </exception>
        /// <exception cref="System.Security.SecurityException:">
        ///     Thrown when The caller does not have the required permission.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        ///     The file cannot be found, such as when mode is FileMode.Truncate or FileMode.Open,
        ///     and the file specified by path does not exist. The file must already exist in
        ///     these modes.
        /// </exception>
        /// <exception cref="IOException">
        ///     An I/O error, such as specifying FileMode.CreateNew when the file specified by
        ///     path already exists, occurred.-or-The stream has been closed.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     The specified path is invalid, such as being on an unmapped drive.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     The specified path, file name, or both exceed the system-defined maximum length.
        ///     For example, on Windows-based platforms, paths must be less than 248 characters,
        ///     and file names must be less than 260 characters.
        /// </exception>
        /// 
        /// <returns>FileStream in FileMode.Open mode</returns>
        public Stream OpenFile()
        {
            return new FileStream(Path, FileMode.Open);
        }
    }
}
