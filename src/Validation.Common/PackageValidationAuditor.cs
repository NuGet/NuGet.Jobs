﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet.Jobs.Validation.Common.Extensions;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationAuditor
    {
        private readonly CloudBlobContainer _auditsContainer;
        private readonly ILogger<PackageValidationAuditor> _logger;

        public PackageValidationAuditor(CloudStorageAccount cloudStorageAccount, string containerNamePrefix)
        {
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            _auditsContainer = cloudBlobClient.GetContainerReference(containerNamePrefix + "-audit");
            _auditsContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            _logger = LoggingSetup.CreateLoggerFactory().CreateLogger<PackageValidationAuditor>();
        }

        public async Task StartAuditAsync(Guid validationId, string[] validators, DateTimeOffset started, string packageId, string packageVersion, NuGetPackage package)
        {
            _logger.LogInformation("Start writing Start PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId,
                package.Id,
                packageVersion);

            var packageValidationAudit = new PackageValidationAudit();
            packageValidationAudit.ValidationId = validationId;
            packageValidationAudit.PackageId = packageId;
            packageValidationAudit.PackageVersion = packageVersion;
            packageValidationAudit.Package = package;
            packageValidationAudit.Started = started;
            packageValidationAudit.Validators = validators;

            await StoreAuditAsync(validationId, packageValidationAudit.PackageId, packageValidationAudit.PackageVersion,
                _ => packageValidationAudit);

            _logger.LogInformation("Finished writing Start PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.", 
                validationId, 
                package.Id, 
                packageVersion);
        }

        public async Task WriteAuditEntryAsync(Guid validationId, string packageId, string packageVersion, PackageValidationAuditEntry entry)
        {
            await WriteAuditEntriesAsync(validationId, packageId, packageVersion, new[] { entry });
        }

        public async Task WriteAuditEntriesAsync(Guid validationId, string packageId, string packageVersion, IEnumerable<PackageValidationAuditEntry> entries)
        {
            _logger.LogInformation("Start writing AuditEntry PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId, 
                packageId, 
                packageVersion);

            await StoreAuditAsync(validationId, packageId, packageVersion,
                packageValidationAudit =>
                {
                    packageValidationAudit.Entries.AddRange(entries);
                    return packageValidationAudit;
                });

            _logger.LogInformation("Finished writing AuditEntry PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.",
                validationId,
                packageId,
                packageVersion);
        }

        public async Task CompleteAuditAsync(Guid validationId, DateTimeOffset completed, string packageId, string packageVersion)
        {
            _logger.LogInformation("Start writing Complete PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId, 
                packageId, 
                packageVersion);

            await StoreAuditAsync(validationId, packageId, packageVersion, 
                packageValidationAudit =>
                {
                    packageValidationAudit.Completed = completed;
                    return packageValidationAudit;
                });

            _logger.LogInformation("Finished writing Complete PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.",
                validationId,
                packageId,
                packageVersion);
        }

        public async Task StoreAuditAsync(Guid validationId, string packageId, string packageVersion, Func<PackageValidationAudit, PackageValidationAudit> updateAudit)
        {
            var blob = _auditsContainer.GetBlockBlobReference(
                         GenerateAuditFileName(
                             validationId,
                             packageId,
                             packageVersion));

            var leaseId = await blob.TryAcquireLeaseAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
            var accessCondition = !string.IsNullOrEmpty(leaseId)
                ? AccessCondition.GenerateLeaseCondition(leaseId)
                : null;

            PackageValidationAudit packageValidationAudit;

            if (await blob.ExistsAsync())
            {
                var json = await blob.DownloadTextAsync(Encoding.UTF8, accessCondition, null, null);
                packageValidationAudit = JsonConvert.DeserializeObject<PackageValidationAudit>(json);
                packageValidationAudit = updateAudit(packageValidationAudit);
            }
            else
            {
                packageValidationAudit = updateAudit(null);
            }

            await blob.UploadTextAsync(JsonConvert.SerializeObject(packageValidationAudit), Encoding.UTF8, accessCondition, null, null);

            blob.Properties.ContentType = "application/json";
            await blob.SetPropertiesAsync(accessCondition, null, null);

            if (accessCondition != null)
            {
                try
                {
                    await blob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
                }
                catch
                {
                    // intentional, lease may already have been expired
                }
            }
        }

        public async Task<PackageValidationAudit> ReadAuditAsync(Guid validationId, string packageId, string packageVersion)
        {
            var blob = _auditsContainer.GetBlockBlobReference(
                         GenerateAuditFileName(
                             validationId,
                             packageId,
                             packageVersion));
            
            if (await blob.ExistsAsync())
            {
                var json = await blob.DownloadTextAsync();
                return JsonConvert.DeserializeObject<PackageValidationAudit>(json);
            }
            else
            {
                return null;
            }
        }

        private static string GenerateAuditFileName(Guid validationId, string packageId, string packageVersion)
        {
            return $"{packageId}/{packageVersion}/{validationId}.json";
        }
    }
}
