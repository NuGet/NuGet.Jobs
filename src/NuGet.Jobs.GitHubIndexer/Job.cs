// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class Job : JsonConfigurationJob
    {
        private const string GitHubSearcherConfigurationSectionName = "GitHubSearcher";
        private const string GitHubIndexerConfigurationSectionName = "GitHubIndexer";

        public override async Task Run()
        {
            await _serviceProvider.GetRequiredService<ReposIndexer>().Run();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            var assembly = Assembly.GetEntryAssembly();
            var assemblyName = assembly.GetName().Name;
            var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

            services.AddTransient<IGitRepoSearcher, GitHubSearcher>();
            services.AddSingleton<IGitHubClient>(provider => new GitHubClient(new ProductHeaderValue(assemblyName, assemblyVersion)));
            services.AddSingleton<IGitHubSearchWrapper, GitHubSearchWrapper>();
            services.AddSingleton<RepoUtils>();
            services.AddSingleton<ReposIndexer>();
            services.AddSingleton<IRepositoriesCache, DiskRepositoriesCache>();
            services.AddSingleton<IConfigFileParser, ConfigFileParser>();
            services.AddSingleton<IRepoFetcher, RepoFetcher>();

            services.Configure<GitHubSearcherConfiguration>(configurationRoot.GetSection(GitHubSearcherConfigurationSectionName));
            services.Configure<GitHubIndexerConfiguration>(configurationRoot.GetSection(GitHubIndexerConfigurationSectionName));
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}