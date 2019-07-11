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
using static NuGet.Jobs.GitHubIndexer.RepoUtils;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private const string WorkingDirectory = "workdir";
        private static readonly string GitHubUsageFilePath = WorkingDirectory + Path.DirectorySeparatorChar + "GitHubUsage.v1.json";
        public static readonly string ExecutionDirectory = WorkingDirectory + Path.DirectorySeparatorChar + "exec";

        private readonly IGitRepoSearcher _searcher;
        private readonly ILogger<ReposIndexer> _logger;
        private readonly int _maxDegreeOfParallelism;
        private readonly IRepositoriesCache _repoCache;
        private readonly IRepoFetcher _repoFetcher;
        private readonly IConfigFileParser _configFileParser;

        public ReposIndexer(IGitRepoSearcher searcher,
            ILogger<ReposIndexer> logger,
            IRepositoriesCache repoCache,
            IConfigFileParser configFileParser,
            IRepoFetcher repoFetcher,
            IOptionsSnapshot<GitHubIndexerConfiguration> configuration)
        {
            _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoCache = repoCache ?? throw new ArgumentNullException(nameof(repoCache));
            _configFileParser = configFileParser ?? throw new ArgumentNullException(nameof(configFileParser));
            _repoFetcher = repoFetcher ?? throw new ArgumentNullException(nameof(repoFetcher));

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

            // Create the exec directory
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

            File.WriteAllText(GitHubUsageFilePath, JsonConvert.SerializeObject(finalList));
            
            // Delete the exec directory
            Directory.CreateDirectory(ExecutionDirectory);
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            if (_repoCache.TryGetCachedVersion(repo, out var cachedVersion))
            {
              return cachedVersion;
            }

            _logger.LogInformation("Starting indexing for repo {name}", repo.Id);
            using (IFetchedRepo fetchedRepo = _repoFetcher.FetchRepo(repo)) {
                var filePaths = fetchedRepo.GetFileInfos(); // Paths in the Git Repo
                var checkedOutFiles = 
                    fetchedRepo.CheckoutFiles(
                        filePaths
                        .Where(x => Filters.GetConfigFileType(x.Path) != Filters.ConfigFileType.None) // TODO: Filter by blobSize too!
                        .Select(x => x.Path) 
                        .ToList()); // List of Git files that are on-disk

                foreach (var cfgFile in checkedOutFiles) {
                    var dependencies = _configFileParser.Parse(cfgFile);
                    repo.AddDependencies(dependencies);
                }
            }

            var result = repo.ToRepositoryInformation();
            _repoCache.Persist(result);
            return result;
             ///////////////////////////////////////////////////////////////////////
             ///////////////////////////////////////////////////////////////////////
             ///////////////////////////////////////////////////////////////////////
             ///////////////////////////////////////////////////////////////////////
             ///////////////////////////////////////////////////////////////////////
             ///////////////////////////////////////////////////////////////////////
            //_logger.LogInformation("Starting indexing for repo {name}", repo.Id);

            //var repoFolder = ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            //var repoCacheFile = repoFolder + "-Cache.json";

            //// Clean previous fetched stuff if it's there (to avoid checkout conflicts)
            //CleanDirectory(new DirectoryInfo(repoFolder));

            //// Check Repo cache file
            //if (File.Exists(repoCacheFile))
            //{
            //    repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
            //    return repo.ToRepositoryInformation();
            //}

            //// Init an empty Git Repo
            //LibGit2Sharp.Repository.Init(repoFolder);
            //using (var localRepo = new LibGit2Sharp.Repository(repoFolder))
            //{
            //    //// Add the origin remote
            //    //localRepo.Network.Remotes.Add("origin", repo.Url);

            //    //var remote = localRepo.Network.Remotes["origin"];
            //    //// Get the HEAD ref to only fetch the main branch
            //    //var headRef = new string[] { "refs/heads/" + repo.MainBranch };

            //    //// Fetch
            //    //LibGit2Sharp.Commands.Fetch(localRepo, remote.Name, headRef, null, "");

            //    // Get the files tree
            //                    //string mainBranchRef = "refs/remotes/origin/" + repo.MainBranch;
            //                    //var fileTree = localRepo.Branches[mainBranchRef].Commits.ToList()[0].Tree;
            //                    //var fullPath = Path.GetFullPath(repoFolder);
            //                    //var filesToParse = _repoUtils
            //                    //    .ListTree(fileTree, "", localRepo, file => Filters.GetConfigFileType(file.Path) != Filters.ConfigFileType.None)
            //                    //    .Where(f => (fullPath + Path.DirectorySeparatorChar + f.Path).Length < 260)
            //                    //    .ToList();

            //    if (filesToParse.Any())
            //    {
            //        _logger.LogInformation("[{RepoName}] Found {0} config files.", repo.Id, filesToParse.Count);

            //        // Checkout the files
            //        localRepo.CheckoutPaths(mainBranchRef, filesToParse.Select(f => f.Path), new LibGit2Sharp.CheckoutOptions());

            //        // Parse files and add them to the repo
            //        repo.AddDependencies(filesToParse
            //            .SelectMany(file =>
            //            {
            //                _logger.LogDebug("[{RepoName}] Parsing file: {FileName}", repo.Id, file.Path);
            //                using (var fileStream = new FileStream(Path.Combine(repoFolder, file.Path), FileMode.Open))
            //                {
            //                    List<string> res;
            //                    if (Filters.GetConfigFileType(file.Path) == Filters.ConfigFileType.PkgConfig)
            //                    {
            //                        res = _repoUtils.ParsePackagesConfig(fileStream, repo.Id);
            //                    }
            //                    else
            //                    {
            //                        res = _repoUtils.ParseProjFile(fileStream, repo.Id);
            //                    }

            //                    _logger.LogDebug("[{RepoName}] Found {Count} dependencies!", repo.Id, res.Count);

            //                    return res;
            //                }
            //            }));
            //    }
            //}

            //var result = repo.ToRepositoryInformation();

            //// Write the cache file. In case the job crashes, we can resume the progress
            //File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(result.Dependencies));
            //CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!

            //return result;
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
