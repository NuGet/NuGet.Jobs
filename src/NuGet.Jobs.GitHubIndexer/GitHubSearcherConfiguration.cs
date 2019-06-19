using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcherConfiguration
    {
        public int MinStars { get; set; } = 100;

        public int ResultsPerPage { get; set; } = 100;

        public int MaxGithubResultPerQuery { get; set; } = 1000;

    }
}
