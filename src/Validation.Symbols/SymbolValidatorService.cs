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
    public class SymbolValidatorService : ISymbolValidatorService
    {
        private const string SymbolWorkingDirectory = "SymbolsValidator";

        private static readonly string[] PEExtensionsPatterns = new string[] { "*.dll", "*.exe" };
        private static readonly string SymbolExtensionPattern = "*.pdb";
        private static readonly string[] PEExtensions = new string[] { ".dll", ".exe" };
        private static readonly string SymbolExtension = ".pdb";

        private readonly ISymbolFileService _symybolFileService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolValidatorService> _logger;

        public SymbolValidatorService(
            ISymbolFileService symybolFileService,
            ITelemetryService telemetryService,
            ILogger<SymbolValidatorService> logger)
        {
            _symybolFileService = symybolFileService ?? throw new ArgumentNullException(nameof(symybolFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Interface methods
        public async Task<IValidationResult> ValidateSymbolsAsync(string packageId, string packageNormalizedVersion, CancellationToken token)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var snupkgstream = await _symybolFileService.DownloadSnupkgFileAsync(packageId, packageNormalizedVersion, token);
            if(snupkgstream == null)
            {
                _telemetryService.TrackSymbolNotFoundEvent(packageId, packageNormalizedVersion);
                return ValidationResult.Failed;
            }
            var nupkgstream = await _symybolFileService.DownloadNupkgFileAsync(packageId, packageNormalizedVersion, token);
            if (nupkgstream == null)
            {
                _telemetryService.TrackPackageNotFoundEvent(packageId, packageNormalizedVersion);
            }
            var pdbs = ReadFilesFromZipStream(snupkgstream, SymbolExtension);
            var pes = ReadFilesFromZipStream(nupkgstream, PEExtensions);

            if (!SymbolsHaveMatchingPEFiles(pdbs, pes))
            {
                // ToDo - change when server common updated                
                return ValidationResult.FailedWithIssues(ValidationIssue.Unknown);
                // MatchingPortablePDBNotFound;
            }

            var targetDirectory = GetWorkingDirectory();
            try
            {
                var symbolFiles = ExtractFilesFromZipStream(snupkgstream, targetDirectory);
                ExtractFilesFromZipStream(nupkgstream, targetDirectory, symbolFiles);

                var status = ValidateSymbolMatching(targetDirectory, packageId, packageNormalizedVersion, PEExtensionsPatterns);
                sw.Stop();
                _telemetryService.TrackSymbolValidationTimeEvent(packageId, packageNormalizedVersion, sw.ElapsedMilliseconds / 1000, pdbs.Count);
                return status;
            }
            finally
            {
                TryCleanWorkingDirectory(targetDirectory);
            }
        }


        /// <summary>
        /// Returns the files from a zip stream. The results are filtered for the files with the specified exceptions.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="matchingExtensions"></param>
        /// <returns></returns>
        public HashSet<string> ReadFilesFromZipStream(Stream stream, params string[] matchingExtensions)
        {
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return ReadFilesFromZipStream(archive.Entries, matchingExtensions);
            }
        }


        public HashSet<string> ExtractFilesFromZipStream(Stream stream, string targetDirectory, HashSet<string> symbolFilter = null)
        {
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return Extract(archive.Entries, targetDirectory, symbolFilter);
            }
        }

        #endregion

        public virtual string GetWorkingDirectory()
        {
            return Path.Combine(Path.GetTempPath(), SymbolWorkingDirectory, Guid.NewGuid().ToString());
        }

        private void TryCleanWorkingDirectory(string workingDirectory)
        {
            try
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, true);
                }
            }
            catch(Exception e)
            {
                _logger.LogWarning(0, e, "{ValidatorName} :TryCleanWorkingDirectory failed.", ValidatorName.SymbolValidator);
            }
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

        public HashSet<string> Extract(IReadOnlyCollection<ZipArchiveEntry> entries,
          string targetDirectory,
          HashSet<string> symbolFilter = null)
        {
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
        /// The method that performs the actual validation.
        /// More information about checksum algorithm: 
        /// https://github.com/dotnet/corefx/blob/master/src/System.Reflection.Metadata/specs/PE-COFF.md#portable-pdb-checksum 
        /// </summary>
        /// <param name="targetDirectory">The directory used during the current validation.</param>
        /// <param name="packageId">Package Id.</param>
        /// <param name="packageNormalizedVersion">PackageNormalized version.</param>
        /// <param name="peExtensionsPatterns">Extensions patterns used for the known PE files.</param>
        /// <returns></returns>
        public IValidationResult ValidateSymbolMatching(string targetDirectory, string packageId, string packageNormalizedVersion, string[] peExtensionsPatterns)
        {
            foreach (string extension in peExtensionsPatterns)
            {
                foreach (string peFile in Directory.GetFiles(targetDirectory, extension, SearchOption.AllDirectories))
                {
                    using (var peStream = File.OpenRead(peFile))
                    using (var peReader = new PEReader(peStream))
                    {
                        // This checks if portable PDB is associated with the PE file and opens it for reading. 
                        // It also validates that it matches the PE file.
                        // It does not validate that the checksum matches, so we need to do that in the following block.
                        if (peReader.TryOpenAssociatedPortablePdb(peFile, File.OpenRead, out var pdbReaderProvider, out var pdbPath) &&
                           // No need to validate embedded PDB (pdbPath == null for embedded)
                           pdbPath != null)
                        {
                            // Get all checksum entries. There can be more than one. At least one must match the PDB.
                            var checksumRecords = peReader.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum)
                                .Select(e => peReader.ReadPdbChecksumDebugDirectoryData(e))
                                .ToArray();

                            if (checksumRecords.Length == 0)
                            {
                                // ToDo - change when server common updated
                                return ValidationResult.FailedWithIssues(ValidationIssue.Unknown);

                                //ChecksumDoesNotMatch;
                            }

                            var pdbBytes = File.ReadAllBytes(pdbPath);
                            var hashes = new Dictionary<string, byte[]>();

                            using (pdbReaderProvider)
                            {
                                var pdbReader = pdbReaderProvider.GetMetadataReader();
                                int idOffset = pdbReader.DebugMetadataHeader.IdStartOffset;

                                foreach (var checksumRecord in checksumRecords)
                                {
                                    if (!hashes.TryGetValue(checksumRecord.AlgorithmName, out var hash))
                                    {
                                        HashAlgorithmName han = new HashAlgorithmName(checksumRecord.AlgorithmName);
                                        using (var hashAlg = IncrementalHash.CreateHash(han))
                                        {
                                            hashAlg.AppendData(pdbBytes, 0, idOffset);
                                            hashAlg.AppendData(new byte[20]);
                                            int offset = idOffset + 20;
                                            int count = pdbBytes.Length - offset;
                                            hashAlg.AppendData(pdbBytes, offset, count);
                                            hash = hashAlg.GetHashAndReset();
                                        }
                                        hashes.Add(checksumRecord.AlgorithmName, hash);
                                    }
                                    if (checksumRecord.Checksum.ToArray().SequenceEqual(hash))
                                    {
                                        // found the right checksum
                                        return ValidationResult.Succeeded;
                                    }
                                }

                                // Not found any checksum record that matches the PDB.
                                // ToDo - change when server common updated
                                return ValidationResult.FailedWithIssues(ValidationIssue.Unknown);
                                //ChecksumDoesNotMatch;
                            }
                        }
                    }
                    // ToDo - change when server common updated
                    return ValidationResult.FailedWithIssues(ValidationIssue.Unknown);
                    //MatchingPortablePDBNotFound;
                }
            }
            // If did not return there were not any PE files to validate. In this case return error to not proceeed with an ingestion.
            _logger.LogError("{ValidatorName}: There were not any dll or exe files found locally." +
                             "This could indicate an issue in the execution or the package was not correct created. PackageId {PackageId} PackageNormalizedVersion {PackageNormalizedVersion}. " +
                             "SymbolCount: {SymbolCount}",
                             ValidatorName.SymbolValidator,
                             packageId,
                             packageNormalizedVersion,
                             Directory.GetFiles(targetDirectory, SymbolExtensionPattern, SearchOption.AllDirectories));
            // ToDo - change when server common updated
            return ValidationResult.FailedWithIssues(ValidationIssue.Unknown);
            //.MatchingPortablePDBNotFound;
        }

        #region A set of static helper methods
        /// <summary>
        /// Based on the snupkg, nupkg folder structure validate that the symbols have associated binary files.
        /// </summary>
        /// <param name="symbols">Symbol list extracted from the compressed folder.</param>
        /// <param name="PEs">The list of PE files extracted from the compressed folder.</param>
        /// <returns></returns>
        public bool SymbolsHaveMatchingPEFiles(IEnumerable<string> symbols, IEnumerable<string> PEs)
        {
            var symbolsWithoutExtension = RemoveExtension(symbols);
            var PEsWithoutExtensions = RemoveExtension(PEs);
            return !symbolsWithoutExtension.Where(s => !PEsWithoutExtensions.Contains(s)).Any();
        }

        /// <summary>
        /// Removes all the file extensions.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static HashSet<string> RemoveExtension(IEnumerable<string> files)
        {
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
            return string.Concat(Path.GetDirectoryName(file), "\\", Path.GetFileNameWithoutExtension(file));
        }

        /// <summary>
        /// Reads all the entries from a zip streams and filter them based on the set of <paramref name="matchingExtensions"/>.
        /// </summary>
        /// <param name="entries">The <see cref="ZipArchiveEntry"/> collection.</param>
        /// <param name="matchingExtensions">The extensions used for filter.</param>
        /// <returns></returns>
        public static HashSet<string> ReadFilesFromZipStream(IReadOnlyCollection<ZipArchiveEntry> entries, params string[] matchingExtensions)
        {
            return new HashSet<string>(entries.
                    Where(e => !string.IsNullOrEmpty(e.Name)).
                    Where(e => matchingExtensions.Contains(Path.GetExtension(e.FullName.ToLowerInvariant()))).
                    Select(e => e.FullName));
        }
        #endregion 
    }
}
