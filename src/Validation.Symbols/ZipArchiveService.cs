// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;

namespace Validation.Symbols
{
    public class ZipArchiveService : IZipArchiveService
    {
        /// <summary>
        /// Returns the files from a zip stream. The results are filtered for the files with the specified exceptions.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="matchingExtensions"></param>
        /// <returns></returns>
        public HashSet<string> ReadFilesFromZipStream(Stream stream, params string[] matchingExtensions)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return ReadFilesFromZipStream(archive.Entries, matchingExtensions);
            }
        }

        public HashSet<string> ExtractFilesFromZipStream(Stream stream, string targetDirectory, HashSet<string> symbolFilter = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return Extract(archive.Entries, targetDirectory, symbolFilter);
            }
        }

        public HashSet<string> Extract(IReadOnlyCollection<ZipArchiveEntry> entries,
          string targetDirectory,
          HashSet<string> symbolFilter = null)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            if (targetDirectory == null)
            {
                throw new ArgumentNullException(nameof(targetDirectory));
            }
            return new HashSet<string>(entries.
                   Where(e => !string.IsNullOrEmpty(e.Name)).
                   Where((e) =>
                   {
                       if (symbolFilter == null)
                       {
                           return true;
                       }
                       return RemoveExtension(symbolFilter).Contains(RemoveExtension(e.FullName));
                   }).
                   Select((e) =>
                   {
                       OnExtract(e, targetDirectory);
                       return e.FullName;
                   }));
        }

        /// <summary>
        /// Overwrite to not extract the files to the <paramref name="targetDirectory"/>.
        /// </summary>
        /// <param name="entry"><see cref="ZipArchiveEntry" /> entry.</param>
        /// <param name="targetDirectory">The target directory to extract the compressed data.</param>
        public virtual void OnExtract(ZipArchiveEntry entry, string targetDirectory)
        {
            string destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            entry.ExtractToFile(destinationPath);
        }

        /// <summary>
        /// Removes all the file extensions.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static HashSet<string> RemoveExtension(IEnumerable<string> files)
        {
            if(files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            return new HashSet<string>(files.Select((s) =>
            {
                return RemoveExtension(s);
            }));
        }

        /// <summary>
        /// Removes the extension for a file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string RemoveExtension(string file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }
            var directory = Path.GetDirectoryName(file);
            return string.IsNullOrEmpty(directory) ? Path.GetFileNameWithoutExtension(file) : string.Concat(directory, "\\", Path.GetFileNameWithoutExtension(file));
        }

        /// <summary>
        /// Reads all the entries from a zip streams and filter them based on the set of <paramref name="matchingExtensions"/>.
        /// </summary>
        /// <param name="entries">The <see cref="ZipArchiveEntry"/> collection.</param>
        /// <param name="matchingExtensions">The extensions used for filter.</param>
        /// <returns></returns>
        public static HashSet<string> ReadFilesFromZipStream(IReadOnlyCollection<ZipArchiveEntry> entries, params string[] matchingExtensions)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            return new HashSet<string>(entries.
                    Where(e => !string.IsNullOrEmpty(e.Name)).
                    Where(e => matchingExtensions.Contains(Path.GetExtension(e.FullName.ToLowerInvariant()))).
                    Select(e => e.FullName));
        }
    }
}
