// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Configuration;

namespace Gallery.Maintenance
{
    public abstract class DeleteExpiredEntityTask<TEntity> : MaintenanceTask
    {
        private readonly TimeSpan _commandTimeout = TimeSpan.FromMinutes(5);

        public DeleteExpiredEntityTask(ILogger<DeleteExpiredEntityTask<TEntity>> logger)
            : base(logger)
        {
        }

        protected abstract string GetSelectQuery();

        protected abstract string GetDeleteQuery();

        protected virtual int GetDeletedRowsPerKey() => 1;

        protected abstract int GetKey(TEntity entity);

        public override async Task RunAsync(Job job)
        {
            IEnumerable<TEntity> expiredEntities;

            using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                expiredEntities = await connection.QueryWithRetryAsync<TEntity>(
                    GetSelectQuery(),
                    commandTimeout: _commandTimeout,
                    maxRetries: 3);
            }

            var expiredKeys = expiredEntities.Select(GetKey);

            var rowCount = 0;
            var expectedRowCount = expiredEntities.Count() * GetDeletedRowsPerKey();

            if (expectedRowCount > 0)
            {
                using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
                using (var transaction = connection.BeginTransaction())
                using (var command = connection.CreateCommand())
                {
                    var numKeys = 0;
                    var parameters = expiredKeys.Select(c => new SqlParameter("@Key" + numKeys++, SqlDbType.Int) { Value = c }).ToArray();
                    command.Parameters.AddRange(parameters);

                    command.CommandText = string.Format(GetDeleteQuery(), string.Join(",", parameters.Select(p => p.ParameterName)));
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = (int)_commandTimeout.TotalSeconds;
                    command.Transaction = transaction;

                    rowCount = await command.ExecuteNonQueryAsync();

                    transaction.Commit();
                }
            }

            _logger.LogInformation("Deleted {0} expired entities. Expected={1}.", rowCount, expectedRowCount);

            if (expectedRowCount != rowCount)
            {
                throw new Exception($"Expected to delete {expectedRowCount} entities, but only deleted {rowCount}!");
            }
        }
    }
}
