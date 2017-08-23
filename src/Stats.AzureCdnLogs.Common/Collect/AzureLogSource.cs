using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public class AzureLogSource : ILogSource
    {
        CloudStorageAccount _azureAccount;
        public AzureLogSource(string connectionString)
        {
            _azureAccount = CloudStorageAccount.Parse(connectionString);
        }

        public IEnumerable<Uri> GetFiles()
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenReadAsync(Uri uri)
        {
            throw new NotImplementedException();
        }

        public bool TryLock(Uri file)
        {
            throw new NotImplementedException();
        }

        Task<bool> ILogSource.TryDeleteAsync(Uri file)
        {
            throw new NotImplementedException();
        }
    }
}
