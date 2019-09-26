// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Gallery.Maintenance.Models;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;

namespace Gallery.Maintenance
{
    internal class DeleteExpiredVerificationKeysTask : UpdateEntityTask<PackageVerificationKey>
    {
        public DeleteExpiredVerificationKeysTask(ILogger<DeleteExpiredVerificationKeysTask> logger)
            : base(logger)
        {
        }

        protected override string GetSelectQuery() => @"
SELECT s.[CredentialKey], c.[UserKey], u.[Username], c.[Expires], s.[Subject] as ScopeSubject
FROM [dbo].[Credentials] c
INNER JOIN [dbo].[Scopes] s ON s.[CredentialKey] = c.[Key]
INNER JOIN [dbo].[Users] u ON u.[Key] = c.[UserKey]
WHERE c.[Type] LIKE 'apikey.verify%' AND c.[Expires] < GETUTCDATE()
";

        protected override string GetUpdateQuery() => @"
DELETE FROM [dbo].[Scopes] WHERE [CredentialKey] IN ({0})
DELETE FROM [dbo].[Credentials] WHERE [Key] IN ({0})
";

        /// <remarks>
        /// Both the credential and the scopes are deleted for each credential key.
        /// </remarks>
        protected override int GetUpdatedRowsPerKey() => 2;

        protected override int GetKey(PackageVerificationKey expiredKey)
        {
            _logger.LogInformation(
                "Found expired verification key: Credential='{credentialKey}' Subject='{scopeSubject}', Expires={expires}",
                expiredKey.CredentialKey, expiredKey.ScopeSubject, expiredKey.Expires);

            return expiredKey.CredentialKey;
        }

        protected override PackageVerificationKey ReadRow(SqlDataReader reader)
        {
            return new PackageVerificationKey
            {
                CredentialKey = reader.GetInt32(0),
                UserKey = reader.GetInt32(1),
                Username = reader.GetString(2),
                Expires = reader.GetDateTime(3),
                ScopeSubject = reader.GetString(4)
            };
        }
    }
}
