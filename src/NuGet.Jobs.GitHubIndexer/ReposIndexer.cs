// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private const string GitHubUsageFileName = "GitHubUsage.v1.json";
        private static readonly string WorkingDirectory = "workdir";
        private static readonly string ExecutionDirectory = WorkingDirectory + Path.DirectorySeparatorChar + "exec";

        private readonly IGitRepoSearcher _searcher;
        private readonly ILogger<ReposIndexer> _logger;
        private readonly RepoUtils _repoUtils;
        private readonly int _maxDegreeOfParallelism;

        public ReposIndexer(IGitRepoSearcher searcher,
            ILogger<ReposIndexer> logger,
            RepoUtils repoUtils,
            IOptionsSnapshot<GitHubIndexerConfiguration> configuration)
        {
            _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _maxDegreeOfParallelism = configuration.Value.MaxDegreeOfParallelism;
        }

        public async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = _maxDegreeOfParallelism;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            var repos = await _searcher.GetPopularRepositories();
            var inputBag = new ConcurrentBag<WritableRepositoryInformation>(repos);
            var outputBag = new ConcurrentBag<RepositoryInformation>();

            Directory.CreateDirectory(ExecutionDirectory);

            await ProcessInParallel(inputBag, repo =>
            {
                try
                {
                    outputBag.Add(ProcessSingleRepo(repo));
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "[{RepoName}] Can't process repo", repo.Id);
                }
            });

            var finalList = outputBag
                .Where(repo => repo.Dependencies.Any())
                .OrderByDescending(x => x.Stars)
                .ThenBy(x => x.Id)
                .ToList();

            File.WriteAllText(GitHubUsageFileName, JsonConvert.SerializeObject(finalList));
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            _logger.LogInformation("Starting indexing for repo {name}", repo.Id);

            var repoFolder = ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            var repoCacheFile = repoFolder + "-Cache.json";

            // Clean previous fetched stuff if it's there (to avoid checkout conflicts)
            CleanDirectory(new DirectoryInfo(repoFolder));

            // Check Repo cache file
            if (File.Exists(repoCacheFile))
            {
                repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
                return repo.ToRepositoryInformation();
            }

            // Init an empty Git Repo
            LibGit2Sharp.Repository.Init(repoFolder);
            using (var localRepo = new LibGit2Sharp.Repository(repoFolder))
            {
                // Add the origin remote
                localRepo.Network.Remotes.Add("origin", repo.Url);

                var remote = localRepo.Network.Remotes["origin"];
                // Get the HEAD ref to only fetch the main branch
                var headRef = new string[] { "refs/heads/" + repo.MainBranch };

                // Fetch
                LibGit2Sharp.Commands.Fetch(localRepo, remote.Name, headRef, null, "");

                // Get the files tree
                string mainBranchRef = "refs/remotes/origin/" + repo.MainBranch;
                var fileTree = localRepo.Branches[mainBranchRef].Commits.ToList()[0].Tree;
                var fullPath = Path.GetFullPath(repoFolder);
                var filesToParse = _repoUtils
                    .ListTree(fileTree, "", localRepo, file => Filters.GetConfigFileType(file.Path) != Filters.ConfigFileType.NONE)
                    .Where(f => (fullPath + Path.DirectorySeparatorChar + f.Path).Length < 260)
                    .ToList();

                if (filesToParse.Any())
                {
                    _logger.LogInformation("[{RepoName}] Found {0} config files.", repo.Id, filesToParse.Count);

                    // Checkout the files
                    localRepo.CheckoutPaths(mainBranchRef, filesToParse.Select(f => f.Path), new LibGit2Sharp.CheckoutOptions());

                    // Parse files and add them to the repo
                    repo.AddDependencies(filesToParse
                        .SelectMany(file =>
                        {
                            _logger.LogDebug("[{RepoName}] Parsing file: {FileName}", repo.Id, file.Path);
                            using (var fileStream = new FileStream(Path.Combine(repoFolder, file.Path), FileMode.Open))
                            {
                                List<string> res;
                                if (Filters.GetConfigFileType(file.Path) == Filters.ConfigFileType.PKG_CONFIG)
                                {
                                    res = _repoUtils.ParsePackagesConfig(fileStream, repo.Id);
                                }
                                else
                                {
                                    res = _repoUtils.ParseProjFile(fileStream, repo.Id);
                                }

                                _logger.LogDebug("[{RepoName}] Found {Count} dependencies!", repo.Id, res.Count);

                                return res;
                            }
                        }));
                }
            }

            var result = repo.ToRepositoryInformation();
            File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(result.Dependencies));
            CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!

            return result;
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

        private async Task ProcessInParallel<T>(ConcurrentBag<T> items, Action<T> work)
        {
            using (var sem = new SemaphoreSlim(_maxDegreeOfParallelism))
            {
                for (int i = 0; i < _maxDegreeOfParallelism; ++i)
                {
                    await sem.WaitAsync();
                    var thread = new Thread(() =>
                        {
                            while (items.TryTake(out var item))
                            {
                                work(item);
                            }
                            sem.Release();
                        })
                    {
                        // This is important as it allows the process to exit while this thread is running
                        IsBackground = true
                    };
                    thread.Start();
                }

                // Wait for all Threads to complete
                for (int i = 0; i < _maxDegreeOfParallelism; ++i)
                {
                    await sem.WaitAsync();
                }
            }

        }
    }
}
