// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class RepoUtils
    {
        private readonly ILogger<RepoUtils> _logger;

        public RepoUtils(ILogger<RepoUtils> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns all the files at a specific commit by listing the file tree
        /// </summary>
        /// <param name="tree">The file tree</param>
        /// <param name="currentPath">The initial file path. (Default is "")</param>
        /// <param name="repo">The repository containing the file tree</param>
        /// <returns>List of nodes that are in the file tree.</returns>
        public IReadOnlyList<GitFileInfo> ListTree(LibGit2Sharp.Tree tree, string currentPath, LibGit2Sharp.Repository repo)
        {
            var files = new List<GitFileInfo>();
            foreach (var node in tree)
            {
                var nodePath = string.IsNullOrWhiteSpace(currentPath) ?
                    node.Path :
                    currentPath + Path.DirectorySeparatorChar + node.Path;

                if (node.TargetType == LibGit2Sharp.TreeEntryTargetType.Tree)
                {
                    files.AddRange(ListTree((LibGit2Sharp.Tree)repo.Lookup(node.Target.Id), nodePath, repo));
                    continue;
                }

                // Only handle files
                if (node.TargetType != LibGit2Sharp.TreeEntryTargetType.Blob)
                {
                    continue;
                }

                var blobSize = ((LibGit2Sharp.Blob)node.Target).Size;
                var nodeInfo = new GitFileInfo(nodePath, blobSize);
                files.Add(nodeInfo);
            }

            return files;
        }

        /// <summary>
        /// Parses a packages.config file and retreives NuGet dependencies in it
        /// </summary>
        /// <param name="fileStream">Stream to the packages.config file</param>
        /// <param name="repoName">The name of the repository containing the file (for logging purposes)</param>
        /// <returns>A list of NuGet packages listed in this config file</returns>
        public IReadOnlyList<string> ParsePackagesConfig(Stream fileStream, string repoName)
        {
            try
            {
                return new PackagesConfigReader(fileStream)
                    .GetPackages()
                    .Select(p => p.PackageIdentity.Id)
                    .Where(Filters.IsValidPackageId)
                    .ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "[{RepoName}] Cannot parse file", repoName);
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Parses any XML file containign PackageReference nodes and retreives those NuGet dependencies
        /// </summary>
        /// <param name="fileStream">Stream to the packages.config file</param>
        /// <param name="repoName">The name of the repository containing the file (for logging purposes)</param>
        /// <returns>A list of NuGet packages listed in this config file</returns>
        public IReadOnlyList<string> ParseProjFile(Stream fileStream, string repoName)
        {
            try
            {
                var projDocument = XDocument.Load(fileStream);
                var refs = projDocument.DescendantNodes().Where(node => node is XElement && ((XElement)node).Name.LocalName.Equals("PackageReference")).Select(n => (XElement)n);
                return refs
                    .Where(p => // Select all that have an "Include" attribute
                    {
                        var includeAttr = p.Attribute("Include");
                        return includeAttr != null && !includeAttr.ToString().Contains("$");
                    })
                    .Select(p => p.Attribute("Include").Value)
                    .Where(Filters.IsValidPackageId)
                    .ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "[{RepoName}] Cannot parse file", repoName);
            }

            return Array.Empty<string>();
        }
    }
}
