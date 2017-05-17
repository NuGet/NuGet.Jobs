using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    public interface IFileStorage
    {
        ISnapshotService SnapshotService { get; }

        PackageHandler GetFile(string id, string version);

        Task DownloadAsync(PackageHandler item, string path);

        Task UploadAsync(PackageHandler item, string path);

        Task BackupAsync(string path, string id, string version, string hash);
    }
}
