// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace CopyAzureContainer
{
    class CopyAzureContainerJob : JobBase
    {
        private readonly string AzCopyPath = @"binaries\azcopy\azCopy.exe";
        private readonly int DefaultBackupDays = -1;
        private string _destStorageAccountName;
        private string _destStorageKeyValue;
        private int _backupDays;

        private IEnumerable<AzureContainerInfo> _sourceContainers;

        public CopyAzureContainerJob() : base()
        {
        }

        public ILogger Logger { get; private set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                Logger = loggerFactory.CreateLogger<CopyAzureContainerJob>();

                _backupDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.CopyAzureContainer_BackupDays)??
                              DefaultBackupDays;
                _destStorageAccountName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.CopyAzureContainer_DestStorageAccountName);
                _destStorageKeyValue = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.CopyAzureContainer_DestStorageKeyValue);
                _sourceContainers = jobArgsDictionary.
                                        Where((kvp) => { return kvp.Key.StartsWith(JobArgumentNames.CopyAzureContainer_SourceContainerPrefix); }).
                                        Select((kvp) => { return new AzureContainerInfo(kvp.Value);});

            }
            catch (Exception exception)
            {
                Logger.LogCritical(LogEvents.JobInitFailed, exception, "Failed to initialize job!");
                return false;
            }

            return true;
        }

        public string GetUsage()
        {
            return "Usage: CopyAzureContainerJob "
                   + $"-{JobArgumentNames.CopyAzureContainer_SourceContainerPrefix}_lucene <storageAccountName:storageAccountKey:ContainerName> More containers with the same prefix can be used"
                   + $"-{JobArgumentNames.CopyAzureContainer_DestStorageAccountName} <destinationStorageAccountName> "
                   + $"-{JobArgumentNames.CopyAzureContainer_DestStorageKeyValue} <destinationStorageAccountKey> "
                   + $"-{JobArgumentNames.CopyAzureContainer_BackupDays} <backupDaysToKeepAsIntValue> "
                   + $"-{JobArgumentNames.InstrumentationKey} <intrumentationKey> "
                   + $"-{JobArgumentNames.VaultName} <keyvault name> "
                   + $"-{JobArgumentNames.ClientId} <keyvault-client-id> "
                   + $"-{JobArgumentNames.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"-{JobArgumentNames.ValidateCertificate} true|false";
        }

        public override async Task<bool> Run()
        {
            DateTimeOffset currentDate = DateTimeOffset.UtcNow;
            var deleteTasks = (_backupDays > 0 ) ? _sourceContainers.Select((c) =>
               { return TryDeleteContainerAsync(c.ContainerName, currentDate, _destStorageAccountName, _destStorageKeyValue); }).ToArray() :
               null;

            foreach(var c in _sourceContainers)
            {
                await TryCopyContainerAsync(c.ContainerName, currentDate, c.StorageAccountName, c.StorageAccountKey);
            }
            if (deleteTasks != null)
            {
                Task.WaitAll(deleteTasks);
            }
            return true;
        }

        private async Task<bool> TryCopyContainerAsync(string container, DateTimeOffset date, string sourceAccountName, string sourceAccountKey)
        {
            Stopwatch sw = new Stopwatch();
            string azCopyTempFolder = $@"{Directory.GetCurrentDirectory()}\azCopy_{container}";
            string destContainer = $"{Format(date)}-{container}";
            string logFile = $"{destContainer}.log";
            string azCopyLogPath = Path.Combine(azCopyTempFolder, logFile);
            RefreshLogData(azCopyTempFolder, azCopyLogPath);

            if (await TryCreateDestinationContainerAsync(destContainer, _destStorageAccountName, _destStorageKeyValue))
            {
                string arguments = $"/Source:https://{sourceAccountName}.blob.core.windows.net/{container}/ " +
                                   $"/Dest:https://{_destStorageAccountName}.blob.core.windows.net/{destContainer}/ " +
                                   $"/SourceKey:{sourceAccountKey} /DestKey:{_destStorageKeyValue} " +
                                   $"/S /Z:{azCopyTempFolder} /V:{azCopyLogPath}";

                string argumentsLog = $"/Source:{azCopyTempFolder} /Dest:https://{_destStorageAccountName}.blob.core.windows.net/logs" +
                                      $" /DestKey:{_destStorageKeyValue} /destType:blob /Pattern:{logFile}";

                try
                {
                    ProcessStartInfo copyToAzureProc = new ProcessStartInfo();
                    copyToAzureProc.FileName = $"{AzCopyPath}";
                    copyToAzureProc.Arguments = $"{arguments}"; 
                    copyToAzureProc.CreateNoWindow = false;
                    copyToAzureProc.UseShellExecute = false;
#if Debug
                copyToAzureProc.RedirectStandardOutput = true;
#endif

                    Logger.LogInformation($"StartContainerCopy:{container}");
                    sw.Start();
                    using (var p = Process.Start(copyToAzureProc))
                    {
                        p.WaitForExit();
#if Debug
                string result = p.StandardOutput.ReadToEnd();
                Console.Write(result);
#endif
                        sw.Stop();
                        int exitCode = p.ExitCode;
                        Logger.LogInformation("EndContainerCopy:{container}:{exitCode}:{elapsedMilliseconds}", container, exitCode, sw.ElapsedMilliseconds);
                        p.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(LogEvents.CopyContainerFailed, ex, "Exception on backup save.");
                    return false;
                }
                try
                {
                    ProcessStartInfo copyToAzureProcLog = new ProcessStartInfo();
                    copyToAzureProcLog.FileName = $"{AzCopyPath}";
                    copyToAzureProcLog.Arguments = $"{argumentsLog}";
                    copyToAzureProcLog.CreateNoWindow = false;
                    copyToAzureProcLog.UseShellExecute = false;
                    using (var pLog = Process.Start(copyToAzureProcLog))
                    {
                        pLog.WaitForExit();
                        pLog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogEvents.CopyLogFailed, ex, "Exception on log save.");
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private void RefreshLogData(string logFolder, string logFile)
        {
            if (Directory.Exists(logFolder))
            {
                Directory.Delete(logFolder, true);
            }
            Directory.CreateDirectory(logFolder);
            using (var stream = File.Create(logFile))
            {
                stream.Close();
            }
        }

        private async Task<bool> TryCreateDestinationContainerAsync(string containerName, string storageAccountName, string storageAccountKey)
        {
            CloudBlobClient blobClient = GetCloudBlobClient(storageAccountName, storageAccountKey);

            try
            {
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                await container.CreateIfNotExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(LogEvents.CreateContainerFailed, ex, "Exception on create container {containerName}.", containerName);
                return false;
            }
        }

        private async Task<bool> TryDeleteContainerAsync(string containerName, DateTimeOffset date, string storageAccountName, string storageAccountKey)
        {
            string destContainer = $"{Format(date.AddDays(-1 * _backupDays))}-{containerName}";
            CloudBlobClient blobClient = GetCloudBlobClient(storageAccountName, storageAccountKey);

            try
            {
                CloudBlobContainer container = blobClient.GetContainerReference(destContainer);
                await container.DeleteIfExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(LogEvents.DeleteContainerFailed, ex, "Exception on delete container {containerName}.", containerName);
                return false;
            }
        }

        private CloudBlobClient GetCloudBlobClient(string storageAccountName, string storageAccountKey)
        {
            string storageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey}";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            return blobClient;
        }

        private string Format(DateTimeOffset date)
        {
            return date.ToString("yyyyMMdd");
        }
    }
}
