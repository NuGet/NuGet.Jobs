namespace NuGet.Jobs.GitHubIndexer
{
    public interface IStampedRepositoryInformation
    {
        /// <summary>
        /// Indicates the git reference hash that the rest of the information on this object was computed for.
        /// </summary>
        string LastKnownSha1 { get; set; }

        /// <summary>
        /// Internal version for stamping
        /// </summary>
        string SchemaVersion { get; set; }
    }
}
