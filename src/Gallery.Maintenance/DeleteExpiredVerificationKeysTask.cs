﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gallery.Maintenance.Models;
using Microsoft.Extensions.Logging;

namespace Gallery.Maintenance
{
    internal class DeleteExpiredVerificationKeysTask : IMaintenanceTask
    {
        private const int DefaultCommandTimeoutInSeconds = 300;
        
        private const string SelectQuery = @"
SELECT s.[CredentialKey], c.[UserKey], c.[Expires], s.[Subject] as ScopeSubject
FROM [dbo].[Credentials] c, [dbo].[Scopes] s
WHERE s.[CredentialKey] = c.[Key] AND c.[Type] LIKE 'apikey.verify%' AND c.[Expires] < GETUTCDATE()";

        private const string DeleteQuery = @"
DELETE FROM [dbo].[Scopes] WHERE [CredentialKey] IN ({0})
DELETE FROM [dbo].[Credentials] WHERE [Key] IN ({0})";

        public async Task<bool> RunAsync(Job job)
        {
            IEnumerable<PackageVerificationKey> expiredKeys;

            using (var connection = await job.GalleryDatabase.ConnectTo())
            {
                expiredKeys = await connection.QueryWithRetryAsync<PackageVerificationKey>(
                    SelectQuery,
                    commandTimeout: DefaultCommandTimeoutInSeconds,
                    maxRetries: 3);
            }

            var credentialKeys = expiredKeys.Select(expiredKey =>
            {
                job.Logger.LogInformation("Deleting expired verification key: Credential='{0}' User='{1}', Subject='{2}', Expired={3}",
                    expiredKey.CredentialKey, expiredKey.UserKey, expiredKey.ScopeSubject, expiredKey.Expires);

                return expiredKey.CredentialKey;
            });

            var rowCount = 0;
            var expectedDeleteCount = expiredKeys.Count();

            if (expectedDeleteCount > 0)
            {
                using (var connection = await job.GalleryDatabase.ConnectTo())
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        rowCount = await connection.ExecuteAsync(
                            string.Format(DeleteQuery, string.Join(",", credentialKeys)),
                            transaction, DefaultCommandTimeoutInSeconds);

                        transaction.Commit();
                    }
                }
            }

            var actualDeleteCount = rowCount / 2;
            job.Logger.LogInformation("Deleted {0} expired verification keys. Expected={1}.", actualDeleteCount, expectedDeleteCount);

            return actualDeleteCount == expectedDeleteCount;
        }
    }
}
