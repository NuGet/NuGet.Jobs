using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    internal class FileSystemSnapshotService : ISnapshotService
    {
        private readonly string _tempDirectory;

        internal FileSystemSnapshotService()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "NuGetService", "HandlePackageEditsSnapshots");
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public async Task<SnapshotHandler> CreateSnapshot(PackageHandler item)
        {
            var src = (FileSystemStorage.FileSystemHandler)item;
            var dest = Path.Combine(_tempDirectory, src.Name);
            return await Task.Run(() =>
            {
                File.Copy(src.FilePath, dest, true);
                return new FileSystemSnapshotHandler(dest);
            });
        }

        public async Task DeleteSnapshotAsync(SnapshotHandler snapshot)
        {
            var item = (FileSystemSnapshotHandler)snapshot;
            await Task.Run(() => File.Delete(item.FilePath));
        }

        public void RestoreSnapshot(PackageHandler item, SnapshotHandler snapshot)
        {
            var src = (FileSystemStorage.FileSystemHandler)item;
            var snapshotItem = (FileSystemSnapshotHandler)snapshot;
            File.Copy(snapshotItem.FilePath, src.FilePath, true);
        }

        private class FileSystemSnapshotHandler : SnapshotHandler
        {
            private readonly string _path;

            public override Uri Uri => new Uri(_path);

            internal string FilePath => _path;

            internal FileSystemSnapshotHandler(string path)
            {
                _path = path;
            }
        }
    }
}
