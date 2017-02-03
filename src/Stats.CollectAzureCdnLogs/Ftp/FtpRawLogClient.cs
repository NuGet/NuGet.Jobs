﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stats.AzureCdnLogs.Common;

namespace Stats.CollectAzureCdnLogs.Ftp
{
    internal sealed class FtpRawLogClient
        : IRawLogClient
    {
        private readonly string _username;
        private readonly string _password;

        public FtpRawLogClient(ILoggerFactory loggerFactory, string username, string password)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            Logger = loggerFactory.CreateLogger<FtpRawLogClient>();

            _username = username;
            _password = password;
        }

        public ILogger Logger { get; private set; }

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

        public async Task<IEnumerable<RawLogFileInfo>> GetRawLogFiles(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var uriString = uri.ToString();

            using (Logger.BeginScope("Beginning directory listing from '{FtpDirectoryUri}'", uriString))
            {
                try
                {
                    var request = CreateRequest(uri);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;
                    var webResponse = (FtpWebResponse) await request.GetResponseAsync();

                    string directoryList;
                    using (var streamReader = new StreamReader(webResponse.GetResponseStream(), Encoding.ASCII))
                    {
                        directoryList = await streamReader.ReadToEndAsync();
                    }

                    Logger.LogInformation("Finishing directory listing.");

                    var fileNames = directoryList.Split(Environment.NewLine.ToCharArray(),
                        StringSplitOptions.RemoveEmptyEntries);
                    var rawLogFiles = fileNames.Select(fn => new RawLogFileInfo(new Uri(uri.EnsureTrailingSlash(), fn)));

                    return rawLogFiles;
                }
                catch (Exception e)
                {
                    Logger.LogError(LogEvents.FailedBlobListing, e, "Failed to get raw log files.");

                    return Enumerable.Empty<RawLogFileInfo>();
                }
            }
        }

        private async Task<FtpStatusCode> GetResponseAsync(FtpWebRequest request)
        {
            for (var attempts = 0; attempts < 5; attempts++)
            {
                try
                {
                    var response = (FtpWebResponse) await request.GetResponseAsync();
                    return response.StatusCode;
                }
                catch (WebException exception)
                {
                    var response = exception.Response as FtpWebResponse;
                    if (response != null)
                    {
                        if (attempts == 4)
                        {
                            return response.StatusCode;
                        }
                    }
                    else
                    {
                        Logger.LogError(LogEvents.FailedToGetFtpResponse, exception, "Failed to get FTP response.");
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
                Logger.LogInformation("Beginning download from '{FtpBlobUri}'.", uri.ToString());
                Trace.TraceInformation("Downloading file '{0}'.", uri);
            }
            else
            {
                Logger.LogInformation("Resuming download from '{FtpBlobUri}' at content offset {ContentOffset}.", uri.ToString(), contentOffset);
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
            request.Credentials = new NetworkCredential(_username, _password);
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