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
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private const int MaxDegreeOfParallelism = 32;//64
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
            ThreadPool.SetMinThreads(MaxDegreeOfParallelism, completionPortThreads: 4);
            ServicePointManager.DefaultConnectionLimit = MaxDegreeOfParallelism;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            var r = JsonConvert.DeserializeObject < IReadOnlyList<RepositoryInformation>>(File.ReadAllText("Repos.json"));
            File.WriteAllText("Repos-org.json", JsonConvert.SerializeObject(r.Where(x => x.Dependencies.Any()).OrderByDescending(x => x.Stars).ThenBy(x => x.Id).ToList()));
            if(true)
            {
                return;
            }

            var repos = await _searcher.GetPopularRepositories();
            var inputBag = new ConcurrentBag<WritableRepositoryInformation>(repos);
            var outputBag = new ConcurrentBag<RepositoryInformation>();

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

            //var processed = repos
            //    .Select(ProcessSingleRepo).ToList();
            var finalList = outputBag
                .Where(repo => repo.Dependencies.Any())
                .ToList();

            var finalList2 = outputBag
                .Where(repo => repo.Dependencies.Count > 0)
                .ToList();

            File.WriteAllText("Repos.json", JsonConvert.SerializeObject(repos));
            File.WriteAllText("Repos-proc.json", JsonConvert.SerializeObject(outputBag));
            File.WriteAllText("FinalRepos.json", JsonConvert.SerializeObject(finalList));
            File.WriteAllText("FinalRepos2.json", JsonConvert.SerializeObject(finalList2));
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            _logger.LogInformation("Starting indexing for repo {name}", repo.Id);

            Directory.CreateDirectory(EXECUTION_DIRECTORY);
            var repoFolder = EXECUTION_DIRECTORY + Path.DirectorySeparatorChar + repo.Id;
            CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!

            // REPO CACHE FILE
            var repoCacheFile = repoFolder + "RepoCache.json";
            if (File.Exists(repoCacheFile))
            {
                repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
            }
            else
            {

                // Init an empty Git Repo
                LibGit2Sharp.Repository.Init(repoFolder);
                using (var localRepo = new LibGit2Sharp.Repository(repoFolder))
                {
                    // Add cloneUrl as the origin remote
                    if (!localRepo.Network.Remotes.Any(r => r.Name == "origin"))
                    {
                        localRepo.Network.Remotes.Add("origin", repo.Url);
                    }

                    // Fetch branches
                    var remote = localRepo.Network.Remotes["origin"];
                    var headRef = new string[] { localRepo.Network.ListReferences(repo.Url).First(x => x.CanonicalName == "HEAD").TargetIdentifier }; // Replace with the result returned from GitHub

                    // Fetch
                    LibGit2Sharp.Commands.Fetch(localRepo, remote.Name, headRef, null, "");

                    // Get the files tree
                    //string mainBranchRef = "refs/remotes/origin/" + headRef[0].Substring(headRef[0].LastIndexOf("/") + 1);
                    string mainBranchRef = "refs/remotes/origin/" + repo.MainBranch;
                    var fileTree = localRepo.Branches[mainBranchRef].Commits.ToList()[0].Tree;
                    var fullPath = Path.GetFullPath(repoFolder);
                    var filesToParse = _repoUtils
                        .ListTree(fileTree, "", localRepo, file =>
                        {
                            //if (file.Path.ToLower().EndsWith("harvestPackages.props"))
                            //{
                            //    _logger.LogWarning("Is Pkg_Cfg: {value} FileName: {fileName} TYPE: {type}", file.Path.ToLower().EndsWith("config"), file.Path, Filters.GetConfigFileType(file.Path));
                            //}
                            //else
                            //{
                            //    //_logger.LogDebug("Is Pkg_Cfg: {value} FileName: {fileName} TYPE: {type}", file.Path.ToLower().EndsWith("config"), file.Path, Filters.GetConfigFileType(file.Path));
                            //}

                            return Filters.GetConfigFileType(file.Path) != Filters.ConfigFileType.NONE;
                        })
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
            }
            File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(repo.Dependencies));
            CleanDirectory(new DirectoryInfo(repoFolder)); //Directory.Delete(repoFolder, true); does not work!

            return repo;
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

        private static async Task ProcessInParallel<T>(ConcurrentBag<T> items, Action<T> work)
        {
            for (int i = 0; i < MaxDegreeOfParallelism; ++i)
            {
                var thread = new Thread(() =>
                    {
                        while (items.TryTake(out var item))
                        {
                            work(item);
                        }
                    })
                {
                    // This is important as it allows the process to exit while this thread is running
                    IsBackground = true
                };
                thread.Start();
            }

            while (!items.IsEmpty)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
            }



            //var tasks = Enumerable
            //    .Range(0, MaxDegreeOfParallelism)
            //    .Select(async i =>
            //    {
            //        await Task.Yield();
            //        while (items.TryTake(out var item))
            //        {
            //            work(item);
            //        }
            //    })
            //    .ToList();

            //await Task.WhenAll(tasks);
        }
    }
}
