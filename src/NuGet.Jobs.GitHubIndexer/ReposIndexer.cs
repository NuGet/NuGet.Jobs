// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private static readonly string WORKING_DIRECTORY = "workdir"; // TODO: Make this in config file?
        private static readonly string EXECUTION_DIRECTORY = WORKING_DIRECTORY + Path.DirectorySeparatorChar + "exec"; // TODO: Make this in config file?

        private readonly IGitRepoSearcher _searcher;

        public ReposIndexer(IGitRepoSearcher searcher)
        {
            _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        }

        public async Task Run()
        {
            var repos = await _searcher.GetPopularRepositories(); // TODO: Create type that RepositoryInformation extends and return it here. That type doesn't have Dependencies
            var finalList = repos
                .Select(ProcessSingleRepo)
                .Where(repo => repo.Dependencies.Any())
                .ToList();

            File.WriteAllText("Repos.json", JsonConvert.SerializeObject(repos));
            File.WriteAllText("FinalRepos.json", JsonConvert.SerializeObject(finalList));
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            Directory.CreateDirectory(EXECUTION_DIRECTORY);
            var repoFolder = EXECUTION_DIRECTORY + Path.DirectorySeparatorChar + repo.Id;

            // Init an empty Git Repo
            LibGit2Sharp.Repository.Init(repoFolder);
            using (var localRepo = new LibGit2Sharp.Repository(repoFolder))
            {
                // Add cloneUrl as the origin remote
                localRepo.Network.Remotes.Add("origin", repo.Url);
                // Fetch branches
                var remote = localRepo.Network.Remotes["origin"];
                var headRef = new string[] { localRepo.Network.ListReferences(repo.Url).First(x => x.CanonicalName == "HEAD").TargetIdentifier }; // Replace with the result returned from GitHub

                // Fetch
                LibGit2Sharp.Commands.Fetch(localRepo, remote.Name, headRef, null, "");

                // Get the files tree
                string mainBranchRef = "refs/remotes/origin/" + headRef[0].Substring(headRef[0].LastIndexOf("/") + 1);
                var fileTree = localRepo.Branches[mainBranchRef].Commits.ToList()[0].Tree;

                var filesToParse = ListTree(fileTree, "", localRepo, fileName => Filters.GetConfigFileType(fileName) != Filters.ConfigFileType.NONE);
                if (filesToParse.Any())
                {
                    // Checkout the files
                    localRepo.CheckoutPaths(mainBranchRef, filesToParse, new LibGit2Sharp.CheckoutOptions());

                    // TODO: Parse files

                }

                CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!
            }

            return repo; // TODO: Change this
        }

        /// <summary>
        /// Returns all the files at a specific commit by listing the file tree
        /// </summary>
        private List<string> ListTree(LibGit2Sharp.Tree tree, string currentPath, LibGit2Sharp.Repository repo, Predicate<string> resultFilter = null)
        {
            List<string> files = new List<string>();
            foreach (var node in tree)
            {
                string nodePath = string.IsNullOrWhiteSpace(currentPath) ? node.Path : currentPath + Path.DirectorySeparatorChar + node.Path;
                if (node.TargetType == LibGit2Sharp.TreeEntryTargetType.Tree)
                {
                    files.AddRange(ListTree((LibGit2Sharp.Tree)repo.Lookup(node.Target.Id), nodePath, repo, resultFilter));
                    continue;
                }

                if (resultFilter == null || (resultFilter != null && resultFilter(node.Path.ToString())))
                {
                    long blobSize = (node.Target as LibGit2Sharp.Blob).Size; // TODO: Use this to limit fileSize!
                    files.Add(nodePath);
                }
            }

            return files;
        }

        /// <summary>
        /// Recursivly deletes all the files and sub-directories in a directory
        /// </summary>
        private static void CleanDirectory(DirectoryInfo dir)
        {

            foreach (var childDir in dir.GetDirectories())
            {
                CleanDirectory(childDir);
            }

            foreach (var file in dir.GetFiles())
            {
                file.IsReadOnly = false;
                file.Delete();
            }

            if (dir.GetFiles().Length == 0)
            {
                dir.Delete();
            }
            else
            {
                Console.Error.WriteLine("The directory {0} is not empty!", dir.FullName);
            }
        }
    }
}
