﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureCdnLogs.Blob;
using Stats.CollectAzureCdnLogs.Configuration;
using Stats.CollectAzureCdnLogs.Ftp;

namespace Stats.CollectAzureCdnLogs
{
    public class Job
         : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string FtpSourceSectionName = "FtpSource";
        private const string AzureCdnSectionName = "AzureCdn";

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);
        private static readonly DateTime _unixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        private IServiceProvider _serviceProvider;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            var configurationRoot = GetConfigurationRoot(configurationFilename, out var secretInjector);
            _serviceProvider = GetServiceProvider(configurationRoot, secretInjector);
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, out ISecretInjector secretInjector)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            secretInjector = null;

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot, ISecretInjector secretInjector)
        {
            var services = new ServiceCollection();

            services.AddSingleton(secretInjector);

            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services, configurationRoot);
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<FtpConfiguration>(configurationRoot.GetSection(FtpSourceSectionName));
            services.Configure<AzureCdnConfiguration>(configurationRoot.GetSection(AzureCdnSectionName));
        }

        private static IServiceProvider CreateProvider(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }


        public override async Task Run()
        {
            // Ensure secrets are refreshed on every run.
            var ftpConfiguration = GetRequiredService<IOptionsSnapshot<FtpConfiguration>>().Value;
            var azureCdnConfiguration = GetRequiredService<IOptionsSnapshot<AzureCdnConfiguration>>().Value;

            // Collect directory listing.
            var ftpClient = new FtpRawLogClient(
                LoggerFactory.CreateLogger<FtpRawLogClient>(),
                ftpConfiguration);
            var rawLogFiles = await ftpClient.GetRawLogFiles(
                azureCdnConfiguration.AccountNumber,
                azureCdnConfiguration.GetAzureCdnPlatform());

            // Prepare cloud storage blob container.
            var azureClient = new CloudBlobRawLogClient(
                LoggerFactory.CreateLogger<CloudBlobRawLogClient>(),
                azureCdnConfiguration);
            var cloudBlobContainer = await azureClient.CreateContainerIfNotExistsAsync();

            foreach (var rawLogFile in rawLogFiles)
            {
                try
                {
                    var skipProcessing = false;
                    var uploadSucceeded = false;
                    var rawLogUri = rawLogFile.Uri;

                    // Check if this is an already renamed file:
                    // This would indicate that the file is being processed already (by another instance of this job),
                    // or that the file is being reprocessed (and the ".download" renamed file was left behind).
                    if (rawLogFile.IsPendingDownload)
                    {
                        // In order to support reprocessing ".gz" files,
                        // we only skip processing ".download" files that have been successfully uploaded to blob storage,
                        // which only happens when they have been processed successfully.
                        // Check if the original ".gz" file has already been uploaded to blob storage.
                        // If it already was uploaded to blob storage,
                        // we can skip processing this ".download" file and delete it from the FTP server.
                        var originalFileName = rawLogFile.FileName.Substring(0, rawLogFile.FileName.Length - FileExtensions.Download.Length);
                        skipProcessing = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, originalFileName);
                    }
                    else
                    {
                        // We are processing a ".gz" file.
                        // Check if the file has already been uploaded to blob storage: are we reprocessing it?
                        var isReprocessing = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, rawLogFile.FileName);

                        if (isReprocessing)
                        {
                            // As we are reprocessing this ".gz" file,
                            // we should first delete the ".download" file if it already exists on the FTP server.
                            var downloadFileUri = new Uri(rawLogFile.Uri + FileExtensions.Download);
                            await ftpClient.DeleteAsync(downloadFileUri);
                        }

                        // Rename the file on the origin to ensure we're not locking a file that still can be written to.
                        var downloadFileName = rawLogFile.FileName + FileExtensions.Download;
                        rawLogUri = await ftpClient.RenameAsync(rawLogFile, downloadFileName);

                        if (rawLogUri == null)
                        {
                            // Failed to rename the file. Leave it and try again later.
                            continue;
                        }
                    }

                    // Skip already processed ".download" files.
                    if (!skipProcessing)
                    {
                        // open the raw log from FTP
                        using (var rawLogStream = await ftpClient.OpenReadAsync(rawLogUri))
                        using (var rawLogStreamInMemory = new MemoryStream())
                        {
                            // copy the raw, compressed stream to memory - FTP does not like reading line by line
                            await rawLogStream.CopyToAsync(rawLogStreamInMemory);
                            rawLogStreamInMemory.Position = 0;

                            // process the raw, compressed memory stream
                            using (var rawGzipStream = new GZipInputStream(rawLogStreamInMemory))
                            {
                                // ensure the .download suffix is trimmed away
                                var fileName = rawLogFile.FileName.Replace(".download", string.Empty);

                                using (Logger.BeginScope("Started uploading file '{FileName}' to {BlobUri}.", fileName, rawLogFile.Uri.ToString()))
                                {
                                    try
                                    {
                                        // open the resulting cleaned blob and stream modified entries
                                        // note the missing using() statement so that we can skip committing if an exception occurs
                                        var resultLogStream = await azureClient.OpenBlobForWriteAsync(cloudBlobContainer, rawLogFile, fileName);

                                        try
                                        {
                                            using (var resultGzipStream = new GZipOutputStream(resultLogStream))
                                            {
                                                resultGzipStream.IsStreamOwner = false;

                                                ProcessLogStream(rawGzipStream, resultGzipStream, fileName);

                                                resultGzipStream.Flush();
                                            }

                                            // commit to blob storage
                                            resultLogStream.Commit();

                                            uploadSucceeded = true;
                                        }
                                        catch
                                        {
                                            uploadSucceeded = false;
                                            throw;
                                        }

                                        Logger.LogInformation("Finished uploading file.");
                                    }
                                    catch (Exception exception)
                                    {
                                        Logger.LogError(
                                            LogEvents.FailedBlobUpload,
                                            exception,
                                            LogMessages.FailedBlobUpload,
                                            rawLogUri);
                                    }
                                }
                            }
                        }
                    }

                    // Delete the renamed file from the origin.
                    if (skipProcessing || uploadSucceeded)
                    {
                        await ftpClient.DeleteAsync(rawLogUri);
                    }
                }
                catch (UnknownAzureCdnPlatformException exception)
                {
                    // Log the failing file, but ignore it. Other files should go through just fine.
                    Logger.LogWarning(
                        LogEvents.UnknownAzureCdnPlatform,
                        exception,
                        LogMessages.UnknownAzureCdnPlatform);
                }
                catch (InvalidRawLogFileNameException exception)
                {
                    // Log the failing file, but ignore it. Other files should go through just fine.
                    Logger.LogWarning(
                        LogEvents.InvalidRawLogFileName,
                        exception,
                        LogMessages.InvalidRawLogFileName);
                }
            }
        }

        private void ProcessLogStream(Stream sourceStream, Stream targetStream, string fileName)
        {
            // note: not using async/await pattern as underlying streams do not support async
            using (var sourceStreamReader = new StreamReader(sourceStream))
            {
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    targetStreamWriter.Write("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");

                    try
                    {
                        var lineNumber = 0;
                        do
                        {
                            var rawLogLine = sourceStreamReader.ReadLine();
                            lineNumber++;

                            var logLine = GetParsedModifiedLogEntry(lineNumber, rawLogLine, fileName);
                            if (!string.IsNullOrEmpty(logLine))
                            {
                                targetStreamWriter.Write(logLine);
                            }
                        }
                        while (!sourceStreamReader.EndOfStream);
                    }
                    catch (SharpZipBaseException e)
                    {
                        // this raw log file may be corrupt...
                        Logger.LogError(LogEvents.FailedToProcessLogStream, e, LogMessages.ProcessingLogStreamFailed);

                        throw;
                    }
                }
            }
        }

        private string GetParsedModifiedLogEntry(int lineNumber, string rawLogEntry, string fileName)
        {
            var parsedEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                lineNumber,
                rawLogEntry,
                (e, line) => Logger.LogError(
                    LogEvents.FailedToParseLogFileEntry,
                    e,
                    LogMessages.ParseLogEntryLineFailed,
                    fileName,
                    line));

            if (parsedEntry == null)
            {
                return null;
            }

            const string spaceCharacter = " ";
            const string dashCharacter = "-";

            var stringBuilder = new StringBuilder();

            // timestamp
            stringBuilder.Append(ToUnixTimeStamp(parsedEntry.EdgeServerTimeDelivered) + spaceCharacter);
            // time-taken
            stringBuilder.Append((parsedEntry.EdgeServerTimeTaken.HasValue ? parsedEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);

            // REMOVE c-ip
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // filesize
            stringBuilder.Append((parsedEntry.FileSize.HasValue ? parsedEntry.FileSize.Value.ToString() : dashCharacter) + spaceCharacter);
            // s-ip
            stringBuilder.Append((parsedEntry.EdgeServerIpAddress ?? dashCharacter) + spaceCharacter);
            // s-port
            stringBuilder.Append((parsedEntry.EdgeServerPort.HasValue ? parsedEntry.EdgeServerPort.Value.ToString() : dashCharacter) + spaceCharacter);
            // sc-status
            stringBuilder.Append((parsedEntry.CacheStatusCode ?? dashCharacter) + spaceCharacter);
            // sc-bytes
            stringBuilder.Append((parsedEntry.EdgeServerBytesSent.HasValue ? parsedEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // cs-method
            stringBuilder.Append((parsedEntry.HttpMethod ?? dashCharacter) + spaceCharacter);
            // cs-uri-stem
            stringBuilder.Append((parsedEntry.RequestUrl ?? dashCharacter) + spaceCharacter);

            // -
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // rs-duration
            stringBuilder.Append((parsedEntry.RemoteServerTimeTaken.HasValue ? parsedEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);
            // rs-bytes
            stringBuilder.Append((parsedEntry.RemoteServerBytesSent.HasValue ? parsedEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // c-referrer
            stringBuilder.Append((parsedEntry.Referrer ?? dashCharacter) + spaceCharacter);
            // c-user-agent
            stringBuilder.Append((parsedEntry.UserAgent ?? dashCharacter) + spaceCharacter);
            // customer-id
            stringBuilder.Append((parsedEntry.CustomerId ?? dashCharacter) + spaceCharacter);
            // x-ec_custom-1
            stringBuilder.AppendLine((parsedEntry.CustomField ?? dashCharacter) + spaceCharacter);

            return stringBuilder.ToString();
        }

        private static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - _unixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
