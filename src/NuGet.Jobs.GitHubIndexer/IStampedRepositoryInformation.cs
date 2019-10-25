namespace NuGet.Jobs.GitHubIndexer
{
    public interface IStampedRepositoryInformation
    {
        string LastKnownSha1 { get; set; }

        string SchemaVersion { get; set; }
    }
}
