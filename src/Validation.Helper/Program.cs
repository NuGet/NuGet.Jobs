using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Helper
{
    class Program
    {
        private static void Main(string[] args)
        {
            if (args.Count() < 4)
            {
                PrintUsage();
                return;
            }

            ISecretReaderFactory secretReaderFactory = new SecretReaderFactory();
            IDictionary<string, string> arguments = JobConfigurationManager.GetJobArgsDictionary(args, "Validation.Helper", secretReaderFactory);

            string azureStorageConnectionString = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.DataStorageAccount);
            string containerName = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.ContainerName);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: {0} " +
                $"-{JobArgumentNames.DataStorageAccount} <Azure Blob Storage connection string> " +
                $"-{JobArgumentNames.ContainerName} <validation job container name>", Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
        }
    }
}
