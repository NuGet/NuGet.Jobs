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
    public abstract class UpdateEntityTask<TEntity> : MaintenanceTask
    {
        private readonly TimeSpan _commandTimeout = TimeSpan.FromMinutes(5);

        public UpdateEntityTask(ILogger<UpdateEntityTask<TEntity>> logger)
            : base(logger)
        {
        }

        protected abstract string GetSelectQuery();

        protected abstract string GetUpdateQuery();

        protected virtual int GetUpdatedRowsPerKey() => 1;

        protected abstract int GetKey(TEntity entity);

        protected abstract TEntity ReadRow(SqlDataReader reader);

        public override async Task RunAsync(Job job)
        {
            using (var connection = await job.OpenSqlConnectionAsync<GalleryDbConfiguration>())
            using (var transaction = connection.BeginTransaction())
            {
                IEnumerable<TEntity> expiredEntities;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = GetSelectQuery();
                    command.CommandTimeout = Convert.ToInt32(_commandTimeout.TotalSeconds);
                    command.CommandType = CommandType.Text;
                    command.Transaction = transaction;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        expiredEntities = await ReadEntities(reader);
                    }
                }

                var expiredKeys = expiredEntities.Select(GetKey);

                var rowCount = 0;
                var expectedRowCount = expiredEntities.Count() * GetUpdatedRowsPerKey();

                if (expectedRowCount > 0)
                {
                    using (var command = connection.CreateCommand())
                    {
                        var numKeys = 0;
                        var parameters = expiredKeys.Select(c => new SqlParameter("@Key" + numKeys++, SqlDbType.Int) { Value = c }).ToArray();
                        command.Parameters.AddRange(parameters);

                        command.CommandText = string.Format(GetUpdateQuery(), string.Join(",", parameters.Select(p => p.ParameterName)));
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = (int)_commandTimeout.TotalSeconds;
                        command.Transaction = transaction;

                        rowCount = await command.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                }

                _logger.LogInformation("Updated {0} entities. Expected={1}.", rowCount, expectedRowCount);

                if (expectedRowCount != rowCount)
                {
                    throw new Exception($"Expected to update {expectedRowCount} entities, but only updated {rowCount}!");
                }
            }
        }

        public async Task<IReadOnlyCollection<TEntity>> ReadEntities(SqlDataReader reader)
        {
            var entities = new List<TEntity>();
            while (reader.HasRows)
            {
                while (await reader.ReadAsync())
                {
                    entities.Add(ReadRow(reader));
                }

                await reader.NextResultAsync();
            }

            return entities;
        }
    }
}
