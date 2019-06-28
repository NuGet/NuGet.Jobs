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
        public class TreeNodeInfo
        {
            public TreeNodeInfo(string path, long blobSize)
            {
                Path = path;
                BlobSize = blobSize;
            }

            public string Path { get; }
            public long BlobSize { get; }
        }

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
        /// <param name="resultFilter">A filter predicate that is used to filter out results. If null, all results are included in the returned list.</param>
        /// <returns>List of nodes that are in the file tree.</returns>
        public List<TreeNodeInfo> ListTree(LibGit2Sharp.Tree tree, string currentPath, LibGit2Sharp.Repository repo, Predicate<TreeNodeInfo> resultFilter = null)
        {
            var files = new List<TreeNodeInfo>();
            foreach (var node in tree)
            {
                var nodePath = string.IsNullOrWhiteSpace(currentPath) ?
                    node.Path :
                    currentPath + Path.DirectorySeparatorChar + node.Path;

                if (node.TargetType == LibGit2Sharp.TreeEntryTargetType.Tree)
                {
                    files.AddRange(ListTree((LibGit2Sharp.Tree)repo.Lookup(node.Target.Id), nodePath, repo, resultFilter));
                    continue;
                }

                // Only handle files
                if (node.TargetType != LibGit2Sharp.TreeEntryTargetType.Blob)
                {
                    continue;
                }

                var blobSize = (node.Target as LibGit2Sharp.Blob).Size;
                var nodeInfo = new TreeNodeInfo(nodePath, blobSize);
                if (resultFilter == null || (resultFilter != null && resultFilter(nodeInfo)))
                {
                    files.Add(nodeInfo);
                }
            }

            return files;
        }

        public List<string> ParsePackagesConfig(Stream fileStream, string repoName)
        {
            try
            {
                return new PackagesConfigReader(fileStream)
                    .GetPackages()
                    .Select(p => p.PackageIdentity.Id)
                    .Where(pId => Filters.IsValidPackageId(pId))
                    .ToList();
            }
            catch (Exception e)
            {
                _logger.LogError("[{RepoName}] Cannot parse file: {Error}", repoName, e.StackTrace);
            }

            return new List<string>();
        }

        public List<string> ParseProjFile(Stream fileStream, string repoName)
        {
            try
            {
                var projDocument = XDocument.Load(fileStream);
                var refs = projDocument.DescendantNodes().Where(node => node is XElement && ((XElement) node ).Name.LocalName.Equals("PackageReference")).Select(n => (XElement) n);

                return refs
                    .Where(p => // Select all that have an "Include" attribute
                    {
                        var includeAttr = p.Attribute("Include");
                        return includeAttr != null && !includeAttr.ToString().Contains("$");
                    })
                    .Select(p => p.Attribute("Include").Value)
                    .Where(pId => Filters.IsValidPackageId(pId))
                    .ToList();
            }
            catch (Exception e)
            {
                _logger.LogError("[{RepoName}] Cannot parse file: {Error}", repoName, e.StackTrace);
            }

            return new List<string>();
        }
    }
}
