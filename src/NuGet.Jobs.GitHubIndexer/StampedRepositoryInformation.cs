using NuGetGallery;
using System.Collections.Generic;

namespace NuGet.Jobs.GitHubIndexer
{
    public class StampedRepositoryInformation : RepositoryInformation, IStampedRepositoryInformation
    {
        public static string CurrentSchemaVersion = "v1";

        public StampedRepositoryInformation(string id, string url, int stars, string description, IReadOnlyList<string> dependencies, string lastKnownSha1, string schemaVersion)
            : base(id, url, stars, description, dependencies)
        {
            LastKnownSha1 = lastKnownSha1;
            SchemaVersion = schemaVersion;
        }

        public string LastKnownSha1 { get; set; }

        public string SchemaVersion { get; set; }
    }
}
