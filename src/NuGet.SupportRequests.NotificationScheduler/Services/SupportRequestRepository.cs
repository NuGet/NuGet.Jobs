// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Models;

namespace NuGet.SupportRequests.NotificationScheduler.Services
{
    internal class SupportRequestRepository
    {
        private const string _parameterNameReferenceTime = "referenceTime";
        private const string _parameterNamePagerDutyUsername = "pagerDutyUserName";
        private readonly DateTime _defaultSqlDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly ILogger _logger;
        private readonly SqlConnectionStringBuilder _sourceDatabase;

        public SupportRequestRepository(
            ILoggerFactory loggerFactory,
            SqlConnectionStringBuilder sourceDatabase)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (sourceDatabase == null)
            {
                throw new ArgumentNullException(nameof(sourceDatabase));
            }

            _logger = loggerFactory.CreateLogger<SupportRequestService>();
            _sourceDatabase = sourceDatabase;
        }

        internal async Task<SqlConnection> OpenConnectionAsync()
        {
            var connection = await _sourceDatabase.ConnectTo();
            connection.InfoMessage += OnSqlConnectionInfoMessage;

            return connection;
        }

        internal async Task<List<SupportRequest>> GetUnresolvedIssues(
            SqlConnection connection,
            string onCallPagerDutyUserName = null)
        {
            connection = await EnsureConnectionOpenAsync(connection);

            var unresolvedIssues = new List<SupportRequest>();

            // Get unresolved issues
            var query = SqlQuery.GetUnresolvedIssues(onCallPagerDutyUserName);
            var sqlCommand = new SqlCommand(query, connection);

            if (!string.IsNullOrEmpty(onCallPagerDutyUserName))
            {
                sqlCommand.Parameters.AddWithValue(_parameterNamePagerDutyUsername, onCallPagerDutyUserName);
            }

            using (var sqlDataReader = await sqlCommand.ExecuteReaderAsync())
            {
                while (await sqlDataReader.ReadAsync())
                {
                    var supportRequest = new SupportRequest();
                    supportRequest.CreatedBy = sqlDataReader.GetString(0);
                    supportRequest.CreatedDate = sqlDataReader.GetDateTime(1);
                    supportRequest.PackageId = sqlDataReader.IsDBNull(2) ? null : sqlDataReader.GetString(2);
                    supportRequest.PackageVersion = sqlDataReader.IsDBNull(3) ? null : sqlDataReader.GetString(3);
                    supportRequest.OwnerEmail = sqlDataReader.GetString(4);
                    supportRequest.Reason = sqlDataReader.GetString(5);
                    supportRequest.PackageRegistrationKey = sqlDataReader.IsDBNull(6) ? (int?)null : sqlDataReader.GetInt32(6);
                    supportRequest.AdminPagerDutyUsername = sqlDataReader.GetString(7);
                    supportRequest.AdminGalleryUsername = sqlDataReader.GetString(8);
                    supportRequest.IssueStatus = sqlDataReader.GetInt32(9);

                    unresolvedIssues.Add(supportRequest);
                }
            }

            return unresolvedIssues;
        }

        internal async Task<int> GetIssueCountCreatedLastWeek(SqlConnection connection, DateTime referenceTime)
        {
            return await ExecuteScalarQueryForReferenceTimeAsync<int>(
                connection,
                SqlQuery.GetIssueCountCreatedLastWeek,
                referenceTime);
        }

        internal async Task<int> GetIssueCountCreatedPriorWeek(SqlConnection connection, DateTime referenceTime)
        {
            return await ExecuteScalarQueryForReferenceTimeAsync<int>(
                connection,
                SqlQuery.GetIssueCountCreatedPriorWeek,
                referenceTime);
        }

        internal async Task<int> GetIssueCountClosedLastWeek(SqlConnection connection, DateTime referenceTime)
        {
            return await ExecuteScalarQueryForReferenceTimeAsync<int>(
                connection,
                SqlQuery.GetIssueCountClosedLastWeek,
                referenceTime);
        }

        internal async Task<int> GetIssueCountClosedPriorWeek(SqlConnection connection, DateTime referenceTime)
        {
            return await ExecuteScalarQueryForReferenceTimeAsync<int>(
                connection,
                SqlQuery.GetIssueCountClosedPriorWeek,
                referenceTime);
        }

        internal async Task<TimeSpan> GetAverageTimeToResolutionLastWeek(SqlConnection connection, DateTime referenceTime)
        {
            var result = await ExecuteScalarQueryForReferenceTimeAsync<DateTime>(
                connection,
                SqlQuery.GetAverageTimeToResolutionLastWeek,
                referenceTime);

            return result - _defaultSqlDateTime;
        }

        internal async Task<TimeSpan> GetAverageTimeToResolutionPriorWeek(SqlConnection connection, DateTime referenceTime)
        {
            var result = await ExecuteScalarQueryForReferenceTimeAsync<DateTime>(
                connection,
                SqlQuery.GetAverageTimeToResolutionPriorWeek,
                referenceTime);

            return result - _defaultSqlDateTime;
        }

        internal async Task<IDictionary<string, int>> GetTopSupportRequestReasonsLastWeek(
            SqlConnection connection,
            DateTime referenceTime)
        {
            connection = await EnsureConnectionOpenAsync(connection);

            var results = new Dictionary<string, int>();

            // Get top support requests last week
            var sqlCommand = new SqlCommand(SqlQuery.GetTopSupportRequestReasonsLastWeek, connection);
            sqlCommand.Parameters.AddWithValue("referenceTime", referenceTime);

            using (var sqlDataReader = await sqlCommand.ExecuteReaderAsync())
            {
                while (await sqlDataReader.ReadAsync())
                {
                    var count = sqlDataReader.GetInt32(0);
                    var reason = sqlDataReader.GetString(1);
                    results.Add(reason, count);
                }
            }

            return results;
        }

        private async Task<T> ExecuteScalarQueryForReferenceTimeAsync<T>(SqlConnection connection, string query, DateTime referenceTime)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            connection = await EnsureConnectionOpenAsync(connection);

            // Get number of issues created in prior week
            var sqlCommand = new SqlCommand(query, connection);
            sqlCommand.Parameters.AddWithValue(_parameterNameReferenceTime, referenceTime);

            return (T)await sqlCommand.ExecuteScalarAsync();
        }

        private async Task<SqlConnection> EnsureConnectionOpenAsync(SqlConnection connection)
        {
            if (connection == null)
            {
                connection = await OpenConnectionAsync();
            }
            else if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            return connection;
        }

        private void OnSqlConnectionInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Message))
            {
                return;
            }

            _logger.LogInformation(e.Message);
        }
    }
}