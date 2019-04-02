// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Gallery.Maintenance
{
    public class DeleteExpiredDeletedAccountsTask : DeleteExpiredEntityTask<ExpiredDeletedAccount>
    {
        public DeleteExpiredDeletedAccountsTask(ILogger<DeleteExpiredDeletedAccountsTask> logger)
            : base(logger)
        {
        }

        protected override string GetSelectQuery() => @"
SELECT u.[Key], u.[Username], d.[DeletedOn]
FROM [dbo].[Users] u
INNER JOIN [dbo].[AccountDeletes] d ON u.[Key] = d.[DeletedAccountKey]
WHERE GETUTCDATE() > DATEADD(year, 1, d.[DeletedOn])
";

        protected override string GetDeleteQuery() => @"
DELETE FROM [dbo].[Users] WHERE [Key] IN ({0})
";

        protected override int GetKey(ExpiredDeletedAccount deletedAccount)
        {
            _logger.LogInformation(
                "Found expired deleted account key deleted on='{deletedOn}'",
                deletedAccount.DeletedOn);

            return deletedAccount.Key;
        }
    }
}
