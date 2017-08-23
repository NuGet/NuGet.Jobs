using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public class AzureLogDestination : ILogDestination
    {
        CloudStorageAccount _azureAccount;

        public AzureLogDestination(string connectionString)
        {
            _azureAccount = CloudStorageAccount.Parse(connectionString);
        }

        public bool TryWriteAsync(Stream stream, Uri destinationFileUri)
        {
            throw new NotImplementedException();
        }
    }
}
