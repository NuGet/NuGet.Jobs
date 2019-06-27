// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private static readonly string WORKING_DIRECTORY = "workdir"; // TODO: Make this in config file?
        private static readonly string EXECUTION_DIRECTORY = WORKING_DIRECTORY + Path.DirectorySeparatorChar + "exec"; // TODO: Make this in config file?

        private readonly IGitRepoSearcher _searcher;
        private readonly ILogger<ReposIndexer> _logger;
        private readonly RepoUtils _repoUtils;

        public ReposIndexer(IGitRepoSearcher searcher, ILogger<ReposIndexer> logger, RepoUtils repoUtils)
        {
            _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
        }

        public async Task Run()
        {
            var repos = await _searcher.GetPopularRepositories();
            var processed = repos
                .Select(ProcessSingleRepo).ToList();
            var finalList = processed
                .Where(repo => repo.Dependencies.Any())
                .ToList();

            File.WriteAllText("Repos.json", JsonConvert.SerializeObject(repos));
            File.WriteAllText("Repos-proc.json", JsonConvert.SerializeObject(repos));
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
                var fullPath = Path.GetFullPath(repoFolder);
                var filesToParse = _repoUtils
                    .ListTree(fileTree, "", localRepo, file =>
                    {
                        if (file.Path.ToLower().EndsWith("harvestPackages.props"))
                        {
                            _logger.LogWarning("Is Pkg_Cfg: {value} FileName: {fileName} TYPE: {type}", file.Path.ToLower().EndsWith("config"), file.Path, Filters.GetConfigFileType(file.Path));
                        }
                        else
                        {
                            //_logger.LogDebug("Is Pkg_Cfg: {value} FileName: {fileName} TYPE: {type}", file.Path.ToLower().EndsWith("config"), file.Path, Filters.GetConfigFileType(file.Path));
                        }

                        return Filters.GetConfigFileType(file.Path) != Filters.ConfigFileType.NONE;
                    })
                    .Where(f => (fullPath + Path.DirectorySeparatorChar + f.Path).Length < 260)
                    .ToList();
                if (filesToParse.Any())
                {
                    _logger.LogInformation("Found {0} config files.", filesToParse.Count);

                    // Checkout the files
                    localRepo.CheckoutPaths(mainBranchRef, filesToParse.Select(f => f.Path), new LibGit2Sharp.CheckoutOptions());

                    // Parse files and add them to the repo
                    repo.AddDependencies(filesToParse
                        .SelectMany(file =>
                        {
                            _logger.LogDebug("[{RepoName}] Parsing file: {FileName}", repo.Id, file.Path);
                            using (var fileStream = new FileStream(Path.Combine(repoFolder, file.Path), System.IO.FileMode.Open))
                            {
                                if (Filters.GetConfigFileType(file.Path) == Filters.ConfigFileType.PKG_CONFIG)
                                {
                                    return _repoUtils.ParsePackagesConfig(fileStream, repo.Id);
                                }
                                else
                                {
                                    return _repoUtils.ParseProjFile(fileStream, repo.Id);
                                }
                            }
                        }));
                }
            }

            CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!

            return repo;
        }



        /// <summary>
        /// Recursivly deletes all the files and sub-directories in a directory
        /// </summary>
        private void CleanDirectory(DirectoryInfo dir)
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
                _logger.LogError("The directory {0} is not empty!", dir.FullName);
            }
        }
    }
}
