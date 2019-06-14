// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace NuGet.Jobs.GitHubIndexer
{
    public class Job : JsonConfigurationJob
    {
        private GitReposSearcher _gitSearcher;
        public Job()
        {
            _gitSearcher = new GitReposSearcher();
        }

        public override async Task  Run()
        {
            // Where the code will be :D
            var repos = await _gitSearcher.GetPopularRepositories();
            File.WriteAllText("Repos.json", JsonConvert.SerializeObject(repos));
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}