// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.AzureCdnLogs.Common
{
    public class AzureBlobLeaseManager
    {
        private ConcurrentDictionary<Uri, string> _leasedBlobs = new ConcurrentDictionary<Uri, string>();
        private BlobRequestOptions _blobRequestOptions;

        public AzureBlobLeaseManager(BlobRequestOptions blobRequestOptions=null)
        {
            _blobRequestOptions = blobRequestOptions;
        }

        /// <summary>
        /// Try to aquire a lease on the blob. If the aquire is sucessful the lease will be renewed at every 60 seconds. 
        /// In order to stop the renew task the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager.TryReleaseLease(CloudBlob)"/> needs to be invoked
        /// or the token to be cancelled.
        /// </summary>
        /// <param name="blob">The blob to aquire the lease on.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <param name="renewStatusTask">The renew task.</param>
        /// <returns></returns>
        public bool AcquireLease(CloudBlob blob, CancellationToken token, out Task<bool> renewStatusTask)
        {
            int maxRenewPeriodInSeconds = 60;
            renewStatusTask = null;
            blob.FetchAttributes();
            if (token.IsCancellationRequested || blob.Properties.LeaseStatus == LeaseStatus.Locked)
            {
                return false;
            }
            var proposedLeaseId = Guid.NewGuid().ToString();
            string leaseId;

            leaseId = blob.AcquireLease(TimeSpan.FromSeconds(maxRenewPeriodInSeconds), proposedLeaseId);
            _leasedBlobs.AddOrUpdate(blob.Uri, leaseId, (uri, guid) => leaseId);

            //start a task that will renew the lease until the token is cancelled or the Release methods was invoked
            renewStatusTask = 
                Task.Run(() =>
                {
                    if(!token.IsCancellationRequested)
                    {
                        //if the token was cancelled just try to release the lease as soon as possible
                        using (token.Register(() => { TryReleaseLease(blob); }))
                        {
                            while (!token.IsCancellationRequested)
                            {
                                string blobLeaseId = string.Empty;
                                //it will renew the lease only of the lease was not explicitelly released 
                                if (_leasedBlobs.TryGetValue(blob.Uri, out blobLeaseId) && blobLeaseId == leaseId)
                                {
                                    Thread.Sleep(maxRenewPeriodInSeconds * 1000);
                                    AccessCondition acc = new AccessCondition() { LeaseId = leaseId };
                                    blob.RenewLease(accessCondition: acc, options: _blobRequestOptions, operationContext: null);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    return true;
                }, token);
            return true;
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
