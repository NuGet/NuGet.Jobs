using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    internal class AzureStorage : IFileStorage
    {
        public const string DefaultSourceContainerName = "packages";
        public const string DefaultBackupContainerName = "package-backups";

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public CloudStorageAccount Source { get; set; }
        public string SourceContainerName { get; set; }
        protected CloudBlobContainer SourceContainer { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the backup storage for package blobs
        /// </summary>
        public CloudStorageAccount Backups { get; set; }
        public string BackupsContainerName { get; set; }
        protected CloudBlobContainer BackupsContainer { get; set; }

        public ISnapshotService SnapshotService { get; private set; }

        public AzureStorage(IDictionary<string, string> jobArgsDictionary)
        {
            Source = CloudStorageAccount.Parse(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceStorage));
            SourceContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.SourceContainerName) ?? DefaultSourceContainerName;
            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(SourceContainerName);
            Backups = CloudStorageAccount.Parse(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.BackupStorage));
            BackupsContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.BackupContainerName) ?? DefaultBackupContainerName;
            BackupsContainer = Backups.CreateCloudBlobClient().GetContainerReference(BackupsContainerName);
            SnapshotService = new AzureSnapshotService();
        }

        public PackageHandler GetFile(string id, string version)
        {
            var sourceBlob = SourceContainer.GetBlockBlobReference(
                StorageHelpers.GetPackageBlobName(id, version));
            return new AzurePackageHandler(sourceBlob);
        }

        public async Task DownloadAsync(PackageHandler item, string path)
        {
            var sourceBlob = ((AzurePackageHandler)item).Blob;
            await sourceBlob.DownloadToFileAsync(path, FileMode.Create);
        }

        public async Task UploadAsync(PackageHandler item, string path)
        {
            var sourceBlob = ((AzurePackageHandler)item).Blob;
            await sourceBlob.UploadFromFileAsync(path);
        }

        public async Task BackupAsync(string path, string id, string version, string hash)
        {
            var backupBlob = BackupsContainer.GetBlockBlobReference(
                    StorageHelpers.GetPackageBackupBlobName(id, version, hash));
            if (!await backupBlob.ExistsAsync())
            {
                await backupBlob.UploadFromFileAsync(path);
            }
        }

        private class AzurePackageHandler : PackageHandler
        {
            private readonly CloudBlockBlob _blob;

            internal CloudBlockBlob Blob => _blob;

            internal AzurePackageHandler(CloudBlockBlob blob)
            {
                _blob = blob;
            }

            public override string Name => _blob.Name;

            public override Uri Uri => _blob.Uri;
        }
    }
}
