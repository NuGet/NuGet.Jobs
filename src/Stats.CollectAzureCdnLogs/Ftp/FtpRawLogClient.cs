// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stats.AzureCdnLogs.Common;

namespace Stats.CollectAzureCdnLogs.Ftp
{
    internal sealed class FtpRawLogClient
        : IRawLogClient
    {
        private const int _maxFtpRequestAttempts = 5;
        private readonly CollectAzureCdnLogsConfiguration _configuration;

        public FtpRawLogClient(
            ILogger<FtpRawLogClient> logger,
            IOptionsSnapshot<CollectAzureCdnLogsConfiguration> configurationAccessor)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }

            _configuration = configurationAccessor.Value;
            AzureCdnPlatform = ConfigurationValidator.ValidateAzureCdnPlatform(_configuration.AzureCdnPlatform);
            AzureCdnAccountNumber = _configuration.AzureCdnAccountNumber;
            FtpSourceUri = ConfigurationValidator.ValidateFtpUri(_configuration.FtpSourceUri);
        }

        public ILogger Logger { get; }
        public AzureCdnPlatform AzureCdnPlatform { get; }
        public string AzureCdnAccountNumber { get; }
        public Uri FtpSourceUri { get; }

        public Task<Stream> OpenReadAsync(Uri uri)
        {
            return Task.FromResult((Stream)new FtpDownloadStream(this, uri));
        }

        public async Task<bool> RenameAsync(Uri uri, string newFileName)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (string.IsNullOrEmpty(newFileName))
            {
                throw new ArgumentNullException(nameof(newFileName));
            }

            using (Logger.BeginScope("Renaming file '{BlobUri}' to '{NewBlobUri}'", uri, newFileName))
            {
                var request = CreateRequest(uri);
                request.Method = WebRequestMethods.Ftp.Rename;
                request.RenameTo = newFileName;

                var result = await GetResponseAsync(request);
                if (result != FtpStatusCode.FileActionOK)
                {
                    // Failed (multiple times) to rename the file on the origin. No point in continuing with this file.
                    Logger.LogError("Failed to rename file. Processing aborted. (FtpStatusCode={FtpStatusCode})", result.ToString());

                    return false;
                }

                return true;
            }
        }

        public async Task DeleteAsync(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var uriString = uri.ToString();
            using (Logger.BeginScope("Deleting file '{BlobUri}'", uriString))
            {
                var request = CreateRequest(uri);
                request.Method = WebRequestMethods.Ftp.DeleteFile;

                var result = await GetResponseAsync(request);
                if (result == FtpStatusCode.FileActionOK)
                {
                    Logger.LogInformation("Finishing delete file.");
                }
                else if (result != FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    // A warning is OK here as the job should retry downloading and processing the file
                    Logger.LogWarning("Failed to delete file. (FtpStatusCode={FtpStatusCode})", result.ToString());
                }
            }
        }

        public async Task<IEnumerable<Uri>> GetRawLogFiles()
        {
            using (Logger.BeginScope("Beginning directory listing from '{FtpDirectoryUri}'", FtpSourceUri.ToString()))
            {
                try
                {
                    var request = CreateRequest(FtpSourceUri);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;
                    var webResponse = (FtpWebResponse)await request.GetResponseAsync();

                    string directoryList;
                    using (var streamReader = new StreamReader(webResponse.GetResponseStream(), Encoding.ASCII))
                    {
                        directoryList = await streamReader.ReadToEndAsync();
                    }

                    Logger.LogInformation("Finishing directory listing.");

                    var fileNames = directoryList.Split(Environment.NewLine.ToCharArray(),
                        StringSplitOptions.RemoveEmptyEntries);
                    return fileNames.Select(fn => new Uri(FtpSourceUri.EnsureTrailingSlash(), fn));
                }
                catch (Exception e)
                {
                    Logger.LogError(LogEvents.FailedBlobListing, e, "Failed to get raw log files.");

                    return Enumerable.Empty<Uri>();
                }
            }
        }

        private async Task<FtpStatusCode> GetResponseAsync(FtpWebRequest request)
        {
            for (var attempts = 0; attempts < _maxFtpRequestAttempts; attempts++)
            {
                try
                {
                    var response = (FtpWebResponse)await request.GetResponseAsync();
                    return response.StatusCode;
                }
                catch (WebException exception)
                {
                    var response = exception.Response as FtpWebResponse;
                    if (response != null)
                    {
                        Logger.LogWarning(
                                LogEvents.FailedToGetFtpResponse,
                                exception,
                                "Captured WebException from FTP response: {ftpStatusCode}. (attempt {attempts}/{maxAttempts})",
                                response.StatusCode,
                                attempts,
                                _maxFtpRequestAttempts);

                        if (attempts == _maxFtpRequestAttempts - 1)
                        {
                            return response.StatusCode;
                        }
                    }
                    else
                    {
                        Logger.LogError(
                            LogEvents.FailedToGetFtpResponse,
                            exception,
                            "Failed to get FTP response. (attempt {attempts}/{maxAttempts})",
                            attempts,
                            _maxFtpRequestAttempts);
                    }
                }
            }

            // This status code is never returned by a real FTP server
            // (if we reach this code, we didn't get a response from the server...)
            return FtpStatusCode.Undefined;
        }

        internal async Task<Stream> StartOrResumeFtpDownload(Uri uri, int contentOffset = 0)
        {
            if (contentOffset == 0)
            {
                Logger.LogInformation("Downloading file '{FtpBlobUri}'.", uri);
            }
            else
            {
                Logger.LogInformation("Resuming download from '{FtpBlobUri}' at content offset {ContentOffset}.", uri, contentOffset);
            }

            var request = CreateRequest(uri);
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            // allow for resume on failure
            request.ContentOffset = contentOffset;

            var webResponse = (FtpWebResponse)await request.GetResponseAsync();
            return webResponse.GetResponseStream();
        }

        private FtpWebRequest CreateRequest(Uri uri)
        {
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(_configuration.FtpSourceUsername, _configuration.FtpSourcePassword);
            request.EnableSsl = true;

            return request;
        }

        public async Task<Uri> RenameAsync(RawLogFileInfo rawLogFile, string newFileName)
        {
            if (rawLogFile == null)
            {
                throw new ArgumentNullException(nameof(rawLogFile));
            }

            if (string.IsNullOrWhiteSpace(newFileName))
            {
                throw new ArgumentNullException(nameof(newFileName));
            }

            Uri rawLogUri;
            if (!rawLogFile.IsPendingDownload)
            {
                if (await RenameAsync(rawLogFile.Uri, newFileName))
                {
                    rawLogUri = new Uri(rawLogFile.Uri + FileExtensions.Download);
                }
                else
                {
                    rawLogUri = null;
                }
            }
            else
            {
                rawLogUri = rawLogFile.Uri;
            }

            return rawLogUri;
        }
    }
}