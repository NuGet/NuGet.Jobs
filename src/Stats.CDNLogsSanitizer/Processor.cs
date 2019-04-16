﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CDNLogsSanitizer
{
    public class Processor
    {
        private readonly ILogger<Processor> _logger;
        private readonly ILogSource _source;
        private readonly ILogDestination _destination;
        private readonly int _maxBatchToProcess;
        private readonly IEnumerable<ISanitizer> _sanitizerList;

        public Processor(ILogSource source, ILogDestination destination, int maxBatchToProcess, IEnumerable<ISanitizer> sanitizerList, ILogger<Processor> logger)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sanitizerList = sanitizerList ?? throw new ArgumentNullException(nameof(sanitizerList));
            _maxBatchToProcess = maxBatchToProcess;
        }

        /// <summary>
        /// Re-writes the blobs from the source to the destination after passing them through the list of sanitizers. 
        /// </summary>
        /// <param name="token">A cancellation token for the async operation.</param>
        /// <param name="blobPrefix">A blob prefix to filter the blobs that will be processed. Use null if no filter needed.</param>
        /// <returns>The awaitable async operation.</returns>
        public async Task ProcessAsync(CancellationToken token, string blobPrefix = null)
        {
            bool continueProcessing = true;
            try
            {
                while (continueProcessing && !token.IsCancellationRequested)
                {
                    var blobs = (await _source.GetFilesAsync(_maxBatchToProcess, token, blobPrefix)).ToArray();
                    continueProcessing = blobs.Length == _maxBatchToProcess;
                    var workers = Enumerable.Range(0, blobs.Length).Select(i => ProcessBlobAsync(blobs[i], token)).ToArray();
                    Task.WaitAll(workers);
                }
            }
            catch (AggregateException aggregateExceptions)
            {
                foreach (var innerEx in aggregateExceptions.InnerExceptions)
                {
                    _logger.LogCritical("ProcessAsync: An exception was encountered. {Exception}", innerEx);
                }
            }
            catch (Exception exception)
            {
                _logger.LogCritical("ProcessAsync: An exception was encountered. {Exception}", exception);
            }
        }

        private async Task ProcessBlobAsync(Uri blobUri, CancellationToken token)
        {
            _logger.LogInformation("ProcessBlobAsync: Start to process blob to {BlobName}.", blobUri.AbsoluteUri);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("ProcessBlobAsync: The operation was cancelled.");
                return;
            }
            var lockResult = await _source.TakeLockAsync(blobUri, token);
            if (lockResult.Item1 /*lockResult*/)
            {
                using (var inputStream = _source.OpenReadAsync(blobUri, ContentType.GZip, token).Result)
                {
                    bool success = await _destination.WriteAsync(inputStream, ProcessStream, blobUri.Segments.Last(), ContentType.GZip, token);
                    await _source.CleanAsync(blobUri, onError: !success, token: token);
                    await _source.ReleaseLockAsync(blobUri, token);
                }
            }
            if (lockResult.Item2 != null && lockResult.Item2.IsFaulted)
            {
                _logger.LogCritical("ProcessBlobAsync: The block renew task had an exception {Exception}", lockResult.Item2.Exception);
            }
            _logger.LogInformation("ProcessBlobAsync: Finished to process blob {BlobName}", blobUri.AbsoluteUri);
        }

        public virtual void ProcessStream(Stream sourceStream, Stream targetStream)
        {
            try
            {
                using (var sourceStreamReader = new StreamReader(sourceStream))
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    bool firstLine = true;
                    while (!sourceStreamReader.EndOfStream)
                    {
                        var rawLogLine = sourceStreamReader.ReadLine();
                        if (rawLogLine != null)
                        {
                            // do not update the header 
                            if (firstLine)
                            {
                                targetStreamWriter.WriteLine(rawLogLine);
                                firstLine = false;
                            }
                            else
                            {
                                string sanitizedLogLine = rawLogLine;

                                foreach (var logSanitizer in _sanitizerList)
                                {
                                    logSanitizer.SanitizeLogLine(ref sanitizedLogLine);
                                }
                                targetStreamWriter.WriteLine(sanitizedLogLine);
                            }
                        }
                    };
                    _logger.LogInformation("ProcessStream: Finished writting to the destination stream.");
                }
            }
            catch (StorageException ex)
            {
                _logger.LogCritical("ProcessStream: An exception while processing the stream {Exception}.", ex);
                throw ex;
            }
        }
    }
}
