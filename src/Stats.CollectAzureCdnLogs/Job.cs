﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.CollectAzureCdnLogs.Blob;
using Stats.CollectAzureCdnLogs.Ftp;

namespace Stats.CollectAzureCdnLogs
{
    public class Job
         : JobBase
    {
        private static readonly DateTime UnixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private Uri _ftpServerUri;
        private string _ftpUsername;
        private string _ftpPassword;
        private string _azureCdnAccountNumber;
        private AzureCdnPlatform _azureCdnPlatform;
        private CloudStorageAccount _cloudStorageAccount;
        private string _cloudStorageContainerName;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var ftpLogFolder = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUri);
                var azureCdnPlatform = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnPlatform);
                var cloudStorageAccount = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                _cloudStorageContainerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName);
                _azureCdnAccountNumber = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnAccountNumber);
                _ftpUsername = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUsername);
                _ftpPassword = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourcePassword);

                _ftpServerUri = ValidateFtpUri(ftpLogFolder);
                _azureCdnPlatform = ValidateAzureCdnPlatform(azureCdnPlatform);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccount);

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }

        private static AzureCdnPlatform ValidateAzureCdnPlatform(string azureCdnPlatform)
        {
            if (string.IsNullOrEmpty(azureCdnPlatform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            AzureCdnPlatform value;
            if (Enum.TryParse(azureCdnPlatform, true, out value))
            {
                return value;
            }
            throw new ArgumentException("Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.");

        }

        private static Uri ValidateFtpUri(string serverUrl)
        {
            var trimmedServerUrl = (serverUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedServerUrl))
            {
                throw new ArgumentException("FTP Server Uri is null or empty.", "serverUrl");
            }

            // if no protocol was specified assume ftp
            var schemeRegex = new Regex(@"^[a-zA-Z]+://");
            if (!schemeRegex.IsMatch(trimmedServerUrl))
            {
                trimmedServerUrl = string.Concat(@"ftp://", trimmedServerUrl);
            }

            var uri = new Uri(trimmedServerUrl);
            if (!uri.IsAbsoluteUri)
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must be an absolute URI. Value: '{0}'.", trimmedServerUrl));
            }

            // only ftp is supported but we could support others
            if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must use the 'ftp://' scheme. Value: '{0}'.", trimmedServerUrl));
            }

            return uri;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var ftpClient = new FtpRawLogClient(JobEventSource.Log, _ftpUsername, _ftpPassword);
                var azureClient = new CloudBlobRawLogClient(JobEventSource.Log, _cloudStorageAccount);

                // Collect directory listing.
                IEnumerable<RawLogFileInfo> rawLogFiles = await ftpClient.GetRawLogFiles(_ftpServerUri);

                // Prepare cloud storage blob container.
                var cloudBlobContainer = await azureClient.CreateContainerIfNotExistsAsync(_cloudStorageContainerName);

                foreach (var rawLogFile in rawLogFiles)
                {
                    try
                    {
                        // Only process the raw log files matching the target CDN platform and account number.
                        if (_azureCdnPlatform == rawLogFile.AzureCdnPlatform && _azureCdnAccountNumber.Equals(rawLogFile.AzureCdnAccountNumber, StringComparison.InvariantCultureIgnoreCase))
                        {
                            bool alreadyUploaded = false;
                            bool uploadSucceeded = false;
                            Uri rawLogUri = rawLogFile.Uri;

                            // Check if this is an already renamed file.
                            if (rawLogFile.IsPendingDownload)
                            {
                                // Check if the file has already been uploaded to blob storage.
                                alreadyUploaded = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, rawLogFile);
                            }
                            else
                            {
                                // Rename the file on the origin to ensure we're not locking a file that still can be written to.
                                rawLogUri = await ftpClient.RenameAsync(rawLogFile, rawLogFile.FileName + FileExtensions.Download);

                                if (rawLogUri == null)
                                {
                                    // Failed to rename the file. Leave it and try again later.
                                    continue;
                                }
                            }

                            if (!alreadyUploaded)
                            {
                                string log;

                                // First decompress and stream into memory for parsing
                                using (var decompressedStream = new MemoryStream())
                                {
                                    using (var rawLogStream = await ftpClient.OpenReadAsync(rawLogUri))
                                    using (var gzipStream = new GZipInputStream(rawLogStream))
                                    {
                                        await gzipStream.CopyToAsync(decompressedStream);
                                    }

                                    // reset the stream's position and read to end
                                    decompressedStream.Position = 0;
                                    using (var streamReader = new StreamReader(decompressedStream))
                                    {
                                        log = await streamReader.ReadToEndAsync();
                                    }
                                }

                                // Parse the data and remove anything we don't want to store
                                string modifiedLog = GetParsedModifiedLog(log);

                                // Upload to blob storage
                                // convert string to stream
                                byte[] byteArray = Encoding.UTF8.GetBytes(modifiedLog);
                                using (var outputStream = new MemoryStream())
                                {
                                    using (var gzipStream = new GZipOutputStream(outputStream))
                                    {
                                        // must synchronously compress in memory because GZipOutputStream has does not support BeginWrite
                                        gzipStream.IsStreamOwner = false;
                                        gzipStream.Write(byteArray, 0, byteArray.Length);
                                        gzipStream.Flush();
                                    }

                                    uploadSucceeded = await azureClient.UploadBlobAsync(cloudBlobContainer, rawLogFile, outputStream);
                                }
                            }

                            // Delete the renamed file from the origin.
                            if (alreadyUploaded || uploadSucceeded)
                            {
                                await ftpClient.DeleteAsync(rawLogUri);
                            }
                        }
                    }
                    catch (UnknownAzureCdnPlatformException exception)
                    {
                        // Trace, but ignore the failing file. Other files should go through just fine.
                        Trace.TraceWarning(exception.ToString());
                    }
                    catch (InvalidRawLogFileNameException exception)
                    {
                        // Trace, but ignore the failing file. Other files should go through just fine.
                        Trace.TraceWarning(exception.ToString());
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }

        private static string GetParsedModifiedLog(string log)
        {
            const string spaceCharacter = " ";
            const string dashCharacter = "-";
            var parsedEntries = ParseLogEntriesFromW3CLog(log);

            var stringBuilder = new StringBuilder("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");
            foreach (var logEntry in parsedEntries)
            {
                // timestamp
                stringBuilder.Append(ToUnixTimeStamp(logEntry.EdgeServerTimeDelivered) + spaceCharacter);
                // time-taken
                stringBuilder.Append((logEntry.EdgeServerTimeTaken.HasValue ? logEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);

                // REMOVE c-ip
                stringBuilder.Append(dashCharacter + spaceCharacter);

                // filesize
                stringBuilder.Append((logEntry.FileSize.HasValue ? logEntry.FileSize.Value.ToString() : dashCharacter) + spaceCharacter);
                // s-ip
                stringBuilder.Append((logEntry.EdgeServerIpAddress ?? dashCharacter) + spaceCharacter);
                // s-port
                stringBuilder.Append((logEntry.EdgeServerPort.HasValue ? logEntry.EdgeServerPort.Value.ToString() : dashCharacter) + spaceCharacter);
                // sc-status
                stringBuilder.Append((logEntry.CacheStatusCode ?? dashCharacter) + spaceCharacter);
                // sc-bytes
                stringBuilder.Append((logEntry.EdgeServerBytesSent.HasValue ? logEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
                // cs-method
                stringBuilder.Append((logEntry.HttpMethod ?? dashCharacter) + spaceCharacter);
                // cs-uri-stem
                stringBuilder.Append((logEntry.RequestUrl ?? dashCharacter) + spaceCharacter);

                // -
                stringBuilder.Append(dashCharacter + spaceCharacter);

                // rs-duration
                stringBuilder.Append((logEntry.RemoteServerTimeTaken.HasValue ? logEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);
                // rs-bytes
                stringBuilder.Append((logEntry.RemoteServerBytesSent.HasValue ? logEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
                // c-referrer
                stringBuilder.Append((logEntry.Referrer ?? dashCharacter) + spaceCharacter);
                // c-user-agent
                stringBuilder.Append((logEntry.UserAgent ?? dashCharacter) + spaceCharacter);
                // customer-id
                stringBuilder.Append((logEntry.CustomerId ?? dashCharacter) + spaceCharacter);
                // x-ec_custom-1
                stringBuilder.AppendLine((logEntry.CustomField ?? dashCharacter) + spaceCharacter);
            }

            return stringBuilder.ToString();
        }

        private static IEnumerable<CdnLogEntry> ParseLogEntriesFromW3CLog(string log)
        {
            var logEntries = new List<CdnLogEntry>();

            var logLines = log.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in logLines)
            {
                var logEntry = ParseLogEntryFromLine(line);
                if (logEntry != null)
                {
                    logEntries.Add(logEntry);
                }
            }

            return logEntries;
        }

        private static CdnLogEntry ParseLogEntryFromLine(string line)
        {
            // ignore comment rows (i.e., first row listing the column headers
            if (line.StartsWith("#"))
                return null;

            // columns are space-separated
            var columns = W3CParseUtils.GetLogLineRecords(line);

            var entry = new CdnLogEntry();

            // timestamp
            entry.EdgeServerTimeDelivered = FromUnixTimestamp(columns[0]);

            // time-taken
            TrySetIntProperty(value => entry.EdgeServerTimeTaken = value, columns[1]);

            // c-ip
            TrySetStringProperty(value => entry.ClientIpAddress = value, columns[2]);

            // filesize
            TrySetLongProperty(value => entry.FileSize = value, columns[3]);

            // s-ip
            TrySetStringProperty(value => entry.EdgeServerIpAddress = value, columns[4]);

            // s-port
            TrySetIntProperty(value => entry.EdgeServerPort = value, columns[5]);

            // sc-status
            TrySetStringProperty(value => entry.CacheStatusCode = value, columns[6]);

            // sc-bytes
            TrySetLongProperty(value => entry.EdgeServerBytesSent = value, columns[7]);

            // cs-method
            TrySetStringProperty(value => entry.HttpMethod = value, columns[8]);

            // cs-uri-stem
            TrySetStringProperty(value => entry.RequestUrl = value, columns[9]);

            // skip column 10, it just contains the '-' character

            // rs-duration
            TrySetIntProperty(value => entry.RemoteServerTimeTaken = value, columns[11]);

            // rs-bytes
            TrySetLongProperty(value => entry.RemoteServerBytesSent = value, columns[12]);

            // c-referrer
            TrySetStringProperty(value => entry.Referrer = value, columns[13]);

            // c-user-agent
            TrySetStringProperty(value => entry.UserAgent = value, columns[14]);

            // customer-id
            TrySetStringProperty(value => entry.CustomerId = value, columns[15]);

            // x-ec_custom-1
            TrySetStringProperty(value => entry.CustomField = value, columns[16]);

            return entry;
        }

        private static void TrySetLongProperty(Action<long?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(long.Parse(record));
            }
        }

        private static void TrySetIntProperty(Action<int?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(int.Parse(record));
            }
        }

        private static void TrySetStringProperty(Action<string> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(record);
            }
        }

        private static DateTime FromUnixTimestamp(string unixTimestamp)
        {
            // Unix timestamp is seconds past epoch
            var secondsPastEpoch = double.Parse(unixTimestamp);
            return UnixTimestamp + TimeSpan.FromSeconds(secondsPastEpoch);
        }

        private static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - UnixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }
    }
}
