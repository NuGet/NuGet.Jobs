// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using static NuGet.Jobs.GitHubIndexer.RepoUtils;

namespace NuGet.Jobs.GitHubIndexer
{
    public class FetchedRepo : IFetchedRepo
    {
        private readonly WritableRepositoryInformation _repoInfo;
        private readonly string _repoFolder;
        private readonly RepoUtils _repoUtils;
        private readonly ILogger _logger;
        private LibGit2Sharp.Repository _repo;

        private FetchedRepo(WritableRepositoryInformation repoInfo, RepoUtils repoUtils, ILogger logger)
        {
            _repoInfo = repoInfo ?? throw new ArgumentNullException(nameof(repoInfo));
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _repoFolder = ReposIndexer.ExecutionDirectory + Path.DirectorySeparatorChar + repoInfo.Id;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // TODO: OMG PLZ REMOVE THIS (Maybe?)
        }

        private void Init()
        {
            CleanDirectory(new DirectoryInfo(_repoFolder));
            LibGit2Sharp.Repository.Init(_repoFolder);
            _repo = new LibGit2Sharp.Repository(_repoFolder);

            // Add the origin remote
            _repo.Network.Remotes.Add("origin", _repoInfo.Url);

            var remote = _repo.Network.Remotes["origin"];
            // Get the HEAD ref to only fetch the main branch
            var headRef = new string[] { "refs/heads/" + _repoInfo.MainBranch };

            _logger.LogTrace("[{RepoName}] Fetching branch {BranchName}.", _repoInfo.Id, _repoInfo.MainBranch);
            // Fetch
            LibGit2Sharp.Commands.Fetch(_repo, remote.Name, headRef, null, "");
        }

        /// <summary>
        /// Persists the specified files to disk.
        /// </summary>
        /// <param name="filePaths">Paths to files in the repository</param>
        /// <returns>List of the persisted files on disk. The list is empty if filePaths is empty.</returns>
        public IReadOnlyList<ICheckedOutFile> CheckoutFiles(IReadOnlyCollection<string> filePaths)
        {
            if (!filePaths.Any())
            {
                return Array.Empty<ICheckedOutFile>();
            }

            _logger.LogInformation("[{RepoName}] Checking out {0} files.", _repoInfo.Id, filePaths.Count);
            string mainBranchRef = "refs/remotes/origin/" + _repoInfo.MainBranch;
            _repo.CheckoutPaths(mainBranchRef, filePaths, new LibGit2Sharp.CheckoutOptions());

            return filePaths.Select(x => new CheckedOutFile(Path.Combine(_repoFolder, x), _repoInfo.Id) as ICheckedOutFile).ToList();
        }

        /// <summary>
        /// Cleans the repository folder and releases LibGit2Sharp hooks to the .git folder
        /// </summary>
        public void Dispose()
        {
            _logger.LogTrace("[{RepoName}] Cleaning repo folder.", _repoInfo.Id);
            _repo.Dispose();
            CleanDirectory(new DirectoryInfo(_repoFolder));
        }

        /// <summary>
        /// Returns a list of all files in the repository that are available in the repo's default branch.
        /// </summary>
        /// <returns>List of files in the repository</returns>
        public IReadOnlyList<GitFileInfo> GetFileInfos()
        {
            string mainBranchRef = "refs/remotes/origin/" + _repoInfo.MainBranch;
            var fileTree = _repo.Branches[mainBranchRef].Commits.ToList()[0].Tree;
            var fullPath = Path.GetFullPath(_repoFolder);

            return _repoUtils
                .ListTree(fileTree, "", _repo)
                .Where(f => (fullPath + Path.DirectorySeparatorChar + f.Path).Length < 260)
                .ToList();
        }

        public static FetchedRepo GetInstance(WritableRepositoryInformation repo, RepoUtils repoUtils, ILogger logger)
        {
            var fetchedRepo = new FetchedRepo(repo, repoUtils, logger);
            fetchedRepo.Init();
            return fetchedRepo;
        }

        /// <summary>
        /// Recursivly deletes all the files and sub-directories in a directory
        /// </summary>
        private void CleanDirectory(DirectoryInfo dir)
        {
            if (!dir.Exists)
            {
                return;
            }
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
                _logger.LogError("The directory {0} is not empty!", dir.FullName);
            }
        }
    }
}
