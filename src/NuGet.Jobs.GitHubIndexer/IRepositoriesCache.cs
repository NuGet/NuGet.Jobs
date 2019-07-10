using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IRepositoriesCache
    {
        bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached);

        void Persist(RepositoryInformation repo);
    }
}
