using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    public interface ISnapshotService
    {
        Task<SnapshotHandler> CreateSnapshot(PackageHandler item);

        void RestoreSnapshot(PackageHandler item, SnapshotHandler snapshot);

        Task DeleteSnapshotAsync(SnapshotHandler snapshot);
    }
}
