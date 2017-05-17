using NuGet.Jobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    internal class FileSystemStorage : IFileStorage
    {
        private readonly string _packageFolder;
        private readonly string _backupFolder;
        private readonly FileSystemSnapshotService _snapshotService;

        public ISnapshotService SnapshotService => _snapshotService;

        public FileSystemStorage(IDictionary<string, string> jobArgsDictionary)
        {
            _packageFolder = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceStorage);
            if (!string.IsNullOrEmpty(JobArgumentNames.BackupStorage))
            {
                _backupFolder = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.BackupStorage);
            }
            _snapshotService = new FileSystemSnapshotService();
        }

        public PackageHandler GetFile(string id, string version)
        {
            var path = Path.Combine(_packageFolder, $"{id}.{version}.nupkg");
            return new FileSystemHandler(path);
        }

        public Task DownloadAsync(PackageHandler item, string path)
        {
            var h = (FileSystemHandler)item;
            File.Copy(h.FilePath, path, true);
            return Task.FromResult(0);
        }

        public Task UploadAsync(PackageHandler item, string path)
        {
            var h = (FileSystemHandler)item;
            File.Copy(path, h.FilePath, true);
            return Task.FromResult(0);
        }

        public Task BackupAsync(string path, string id, string version, string hash)
        {
            if (string.IsNullOrEmpty(_backupFolder))
            {
                return Task.FromResult(0);
            }
            var srcPath = Path.Combine(_packageFolder, $"{id}.{version}.nupkg");
            var destPath = Path.Combine(_backupFolder, $"{id}.{version}.{DateTime.Now.ToString("yyyyMMddHHmmss")}.nupkg");
            File.Copy(srcPath, destPath, true);
            return Task.FromResult(0);
        }

        internal class FileSystemHandler : PackageHandler
        {
            private readonly string _path;

            internal string FilePath => _path;

            public override string Name => Path.GetFileName(_path);

            public override Uri Uri => new Uri(_path);

            public FileSystemHandler(string path)
            {
                _path = path;
            }
        }
    }
}
