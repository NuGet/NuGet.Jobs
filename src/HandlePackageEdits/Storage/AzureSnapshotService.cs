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
    public class AzureSnapshotService : ISnapshotService
    {
        public async Task<SnapshotHandler> CreateSnapshot(PackageHandler item)
        {
            var blob = ((AzurePackageHandler)item).Blob;
            var sourceSnapshot = await blob.CreateSnapshotAsync();
            return new AzureSnapshotHandler(sourceSnapshot);
        }

        public void RestoreSnapshot(PackageHandler item, SnapshotHandler snapshot)
        {
            var blob = ((AzurePackageHandler)item).Blob;
            var snapshotBlob = ((AzureSnapshotHandler)snapshot).Blob;
            blob.StartCopy(snapshotBlob);
        }

        public async Task DeleteSnapshotAsync(SnapshotHandler snapshot)
        {
            var snapshotBlob = ((AzureSnapshotHandler)snapshot).Blob;
            await snapshotBlob.DeleteAsync();
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

        private class AzureSnapshotHandler : SnapshotHandler
        {
            private CloudBlockBlob _blob;

            internal CloudBlockBlob Blob => _blob;

            public override Uri Uri => _blob.Uri;

            internal AzureSnapshotHandler(CloudBlockBlob blob)
            {
                _blob = blob;
            }
        }
    }
}
