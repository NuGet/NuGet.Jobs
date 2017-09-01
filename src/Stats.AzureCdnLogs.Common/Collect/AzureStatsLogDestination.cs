
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using ICSharpCode.SharpZipLib.GZip;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A <see cref="ILogDestination"/> implementation using Azure Storage as the storage. 
    /// </summary>
    public class AzureStatsLogDestination : ILogDestination
    {
        private const string _contentType_Gzip = "application/x-gzip";
        private const string _contentType_Text = "text/plain";

        private CloudStorageAccount _azureAccount;
        private CloudBlobClient _cloudBlobClient;
        private CloudBlobContainer _cloudBlobContainer;

        public AzureStatsLogDestination(string connectionString, string containerName)
        {
            _azureAccount = CloudStorageAccount.Parse(connectionString);
            _cloudBlobClient = _azureAccount.CreateCloudBlobClient();
            _cloudBlobContainer = _cloudBlobClient.GetContainerReference(containerName);
            _cloudBlobContainer.CreateIfNotExists();
        }

        /// <summary>
        /// Writes the input stream to the destination using the writeAction.
        /// If the destinationfile exists the method will be noop.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="writeAction">The write action between the two streams.</param>
        /// <param name="destinationFileName">The destination file name.</param>
        /// <param name="destinationContentType">The destination content type.</param>
        /// <returns></returns>
        public async Task WriteAsync(Stream inputStream, Action<Stream,Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token)
        {
            if(token.IsCancellationRequested)
            {
                return;
            }
            var blob = _cloudBlobContainer.GetBlockBlobReference(destinationFileName);
            if(blob.Exists())
            {
                return;
            }
            blob.Properties.ContentType = GetContentType(destinationContentType);
            var resultStream = await blob.OpenWriteAsync();
            if (destinationContentType == ContentType.GZip)
            {
                using (var resultGzipStream = new GZipOutputStream(resultStream))
                {
                    resultGzipStream.IsStreamOwner = false;
                    writeAction(inputStream, resultGzipStream);
                    await resultGzipStream.FlushAsync();
                }
            }
            else
            {
                writeAction(inputStream, resultStream);
            }
            resultStream.Commit();
        }

        private string GetContentType(ContentType contentType)
        {
            switch(contentType)
            {
                case ContentType.GZip:
                    return _contentType_Gzip;
                case ContentType.Text:
                    return _contentType_Text;
                default:
                    return null;
            }
        }
    }
}
