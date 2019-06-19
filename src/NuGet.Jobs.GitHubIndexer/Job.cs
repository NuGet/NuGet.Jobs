// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class Job : JsonConfigurationJob
    {
        private const string GitHubIndexerName = "GitHubIndexer";
        private const string GitHubSearcherConfigurationSectionName = "GitHubSearcher";

        public override async Task  Run()
        {
            var searcher = _serviceProvider.GetRequiredService<IGitRepoSearcher>();
            var repos = await searcher.GetPopularRepositories();

            File.WriteAllText("Repos.json", JsonConvert.SerializeObject(repos));
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddTransient<IGitRepoSearcher, GitHubSearcher>();
            services.AddSingleton<IGitHubClient>(provider => 
            {
                return new GitHubClient(new ProductHeaderValue(GitHubIndexerName));
            });

            services.Configure<GitHubSearcherConfiguration>(configurationRoot.GetSection(GitHubSearcherConfigurationSectionName));
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}