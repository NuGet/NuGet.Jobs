// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.AzureCdnLogs.Common
{
    /// <summary>
    /// Manages lease acquisition on Azure Blobs.
    /// A lease, once is acquired, it will be continuously renewed every 60 minutes.
    /// </summary>
    public class AzureBlobLeaseManager
    {
        private const int MaxRenewPeriodInSeconds = 60;
        // The lease will be renewed with a short interval before the the lease expires
        private const int OverlapRenewPeriodInSeconds = 10;
        private ConcurrentDictionary<Uri, string> _leasedBlobs = new ConcurrentDictionary<Uri, string>();
        private BlobRequestOptions _blobRequestOptions;
        private readonly ILogger<AzureBlobLeaseManager> _logger;

        public AzureBlobLeaseManager(ILogger<AzureBlobLeaseManager> logger, BlobRequestOptions blobRequestOptions = null)
        {
            _blobRequestOptions = blobRequestOptions;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Try to acquire a lease on the blob. If the acquire is successful the lease will be renewed at every 60 seconds. 
        /// In order to stop the renew task the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager.TryReleaseLease(CloudBlob)"/> needs to be invoked
        /// or the token to be cancelled.
        /// </summary>
        /// <param name="blob">The blob to acquire the lease on.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <param name="renewStatusTask">The renew task.</param>
        /// <returns>True if the lease was acquired. </returns>
        public AzureBlobLockResult AcquireLease(CloudBlob blob, CancellationToken token)
        {
            blob.FetchAttributes();
            if (token.IsCancellationRequested || blob.Properties.LeaseStatus == LeaseStatus.Locked)
            {
                _logger.LogInformation("AcquireLease: The operation was cancelled or the blob lease is already taken. Blob {BlobUri}, Cancellation status {IsCancellationRequested}, BlobLeaseStatus {BlobLeaseStatus}.",
                    blob.Uri.AbsoluteUri,
                    token.IsCancellationRequested,
                    blob.Properties.LeaseStatus);
                return AzureBlobLockResult.FailedLockResult();
            }
            var proposedLeaseId = Guid.NewGuid().ToString();

            var leaseId = blob.AcquireLease(TimeSpan.FromSeconds(MaxRenewPeriodInSeconds), proposedLeaseId);
            // If the lease was lost but the _leasedBlobs is not clean it means that a TryReleaseLease was not invoked 
            // This means that an Operation (read => copy => delete blob) was started but not fully completed 
            // One reason for this can be the fact that the task that does the blob lease renew was not succesful and the lease was lost.
            if (!_leasedBlobs.TryAdd(blob.Uri, leaseId))
            {
                var accessCondition = new AccessCondition()
                {
                    LeaseId = leaseId
                };
                // release the lease just taken and return
                blob.ReleaseLease(accessCondition);
                return AzureBlobLockResult.FailedLockResult();
            }

            var lockResult = new AzureBlobLockResult(true, leaseId, token);

            //start a task that will renew the lease until the token is cancelled or the Release methods was invoked
            var renewStatusTask = new Task( (lockresult) =>
                {
                    var blobLockResult = (AzureBlobLockResult)lockresult;
                    _logger.LogInformation("RenewLeaseTask: Started for BlobUri {BlobUri}. ThreadId {ThreadId}. IsCancellationRequested {IsCancellationRequested}. LeaseId {LeaseId}", 
                        blob.Uri.AbsoluteUri,
                        Thread.CurrentThread.ManagedThreadId,
                        blobLockResult.BlobOperationToken.IsCancellationRequested,
                        blobLockResult.LeaseId);

                    int sleepBeforeRenewInSeconds = MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds < 0 ? MaxRenewPeriodInSeconds : MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds;
                    if (!blobLockResult.BlobOperationToken.IsCancellationRequested)
                    {
                        //if the token was cancelled just try to release the lease as soon as possible
                        using (blobLockResult.BlobOperationToken.Token.Register(() => { TryReleaseLease(blob); }))
                        {
                            while (!blobLockResult.BlobOperationToken.Token.IsCancellationRequested)
                            {
                                Thread.Sleep(sleepBeforeRenewInSeconds * 1000);
                               
                                //it will renew the lease only if the lease was not explicitly released 
                                try
                                {
                                    AccessCondition acc = new AccessCondition() { LeaseId = blobLockResult.LeaseId };
                                    blob.RenewLease(accessCondition: acc, options: _blobRequestOptions, operationContext: null);
                                    _logger.LogInformation("RenewLeaseTask: Lease was renewed for BlobUri {BlobUri} and LeaseId {LeaseId}.",
                                        blob.Uri.AbsoluteUri,
                                        blobLockResult.LeaseId);
                                }
                                catch(StorageException)
                                {
                                    _logger.LogInformation("RenewLeaseTask: The Lease could not be renewed for BlobUri {BlobUri}. ExpectedLeaseId {LeaseId}. CurrentLeaseId {CurrentLeaseId}",
                                        blob.Uri.AbsoluteUri,
                                        leaseId,
                                        blobLockResult.LeaseId);
                                    blobLockResult.BlobOperationToken.Cancel();
                                    break;
                                }
                            }
                        }
                    }
                    token.ThrowIfCancellationRequested();
                }, lockResult, token, TaskCreationOptions.LongRunning);
            renewStatusTask.Start();
            return lockResult;
        }

        /// <summary>
        /// Returns true if a lease was taken through the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager"/>
        /// </summary>
        /// <param name="blob"></param>
        /// <param name="leaseId"></param>
        /// <returns></returns>
        public bool HasLease(CloudBlob blob, out string leaseId)
        {
            return _leasedBlobs.TryGetValue(blob.Uri, out leaseId);
        }

        /// <summary>
        /// Returns true if a lease was taken through the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager"/>
        /// </summary>
        /// <param name="blobUri"></param>
        /// <param name="leaseId"></param>
        /// <returns></returns>
        public bool HasLease(Uri blobUri, out string leaseId)
        {
            return _leasedBlobs.TryGetValue(blobUri, out leaseId);
        }

        /// <summary>
        /// It will try to release the lease on this blob if the blob lease was taken through this <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager"/>.
        /// This method will not thorw any exception.
        /// </summary>
        /// <param name="blob">The blob to release the lease from.</param>
        /// <returns>True if the Release was successful.</returns>
        public bool TryReleaseLease(CloudBlob blob)
        {
            var leaseId = string.Empty;
            if (!_leasedBlobs.TryGetValue(blob.Uri, out leaseId))
            {
                return false;
            }
            //after this call the renew task will cease execution 
            _leasedBlobs.TryRemove(blob.Uri, out leaseId);
            try
            {
                AccessCondition acc = new AccessCondition();
                acc.LeaseId = leaseId;
                blob.ReleaseLease(acc, options: _blobRequestOptions, operationContext: null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
