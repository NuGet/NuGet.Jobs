﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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
        private const string WorkingDirectory = "workdir";
        private static readonly string GitHubUsageFilePath = WorkingDirectory + Path.DirectorySeparatorChar + "GitHubUsage.v1.json";
        public static readonly string ExecutionDirectory = WorkingDirectory + Path.DirectorySeparatorChar + "exec";

        private readonly IGitRepoSearcher _searcher;
        private readonly ILogger<ReposIndexer> _logger;
        private readonly int _maxDegreeOfParallelism;
        private readonly IRepositoriesCache _repoCache;
        private readonly IRepoFetcher _repoFetcher;
        private readonly IConfigFileParser _configFileParser;

        public ReposIndexer(
            IGitRepoSearcher searcher,
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

            // TODO: Replace with upload to Azure Blob Storage (https://github.com/NuGet/NuGetGallery/issues/7211)
            File.WriteAllText(GitHubUsageFilePath, JsonConvert.SerializeObject(finalList));

            // Delete the exec directory
            Directory.Delete(ExecutionDirectory, true);
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            if (_repoCache.TryGetCachedVersion(repo, out var cachedVersion))
            {
                return cachedVersion;
            }

            _logger.LogInformation("Starting indexing for repo {name}", repo.Id);
            using (IFetchedRepo fetchedRepo = _repoFetcher.FetchRepo(repo))
            {
                var filePaths = fetchedRepo.GetFileInfos(); // Paths of all files in the Git Repo
                var checkedOutFiles =
                    fetchedRepo.CheckoutFiles(
                        filePaths
                        .Where(x => Filters.GetConfigFileType(x.Path) != Filters.ConfigFileType.None) // TODO: Filter by blobSize too! (https://github.com/NuGet/NuGetGallery/issues/7339)
                        .Select(x => x.Path)
                        .ToList()); // List of config files that are on-disk

                foreach (var cfgFile in checkedOutFiles)
                {
                    var dependencies = _configFileParser.Parse(cfgFile);
                    repo.AddDependencies(dependencies);
                }
            }

            var result = repo.ToRepositoryInformation();
            _repoCache.Persist(result);
            return result;
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
