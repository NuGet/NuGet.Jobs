// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Threading.Tasks;
using System.Web.Security;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace CreateNewSqlUsers
{
    public class CreateNewSqlUsersJob : JobBase
    {
        private const string MasterDatabaseName = "master";
        private const string ReaderRoleName = "ReadOnly";
        private const string WriterRoleName = "Writer";

        private const int PasswordLength = 32;
        private const int PasswordNumNonAlphanumericCharacters = 8;

        private string _primaryServerUrl;
        private string _secondaryServerUrl;
        private string _databaseName;
        private string _primaryAdminUsername;
        private string _primaryAdminPassword;
        private string _secondaryAdminUsername;
        private string _secondaryAdminPassword;
        private string _newPrincipalPrefix;
        private string _readerRole;
        private string _writerRole;
        private bool _whatIf;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            _primaryServerUrl = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.PrimaryServerUrl);
            _secondaryServerUrl = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.SecondaryServerUrl);
            _databaseName = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.DatabaseName);
            _primaryAdminUsername = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.PrimaryAdminUsername);
            _primaryAdminPassword = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.PrimaryAdminPassword);
            _secondaryAdminUsername = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.SecondaryAdminUsername);
            _secondaryAdminPassword = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.SecondaryAdminPassword);
            _newPrincipalPrefix = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.NewPrincipalPrefix);
            _readerRole = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.ReaderRole);
            _writerRole = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.WriterRole);
            _whatIf = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.WhatIf);

            if (JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey) != null)
            {
                throw new ArgumentException("This job should be run locally and shouldn't log to the cloud!");
            }
        }

        public override async Task Run()
        {
            foreach (var roleTuple in new[] { Tuple.Create(ReaderRoleName, _readerRole), Tuple.Create(WriterRoleName, _writerRole) })
            {
                var primaryInfo = await CreateUserAndLoginOnPrimary(_primaryServerUrl, _databaseName, _primaryAdminUsername, _primaryAdminPassword, roleTuple.Item1, roleTuple.Item2);
                var secondaryInfo = await CreateLoginOnSecondary(_secondaryServerUrl, _databaseName, _secondaryAdminUsername, _secondaryAdminPassword, primaryInfo);

                await CheckConnection(_primaryServerUrl, _databaseName, primaryInfo.Username, primaryInfo.Password);
                await CheckConnection(_secondaryServerUrl, _databaseName, secondaryInfo.Username, secondaryInfo.Password);
            }
        }

        private async Task<SqlUserInfo> CreateUserAndLoginOnPrimary(string serverUrl, string databaseName, string adminUser, string adminPassword, string roleName, string role)
        {
            var principal = GeneratePrincipalName(roleName);
            var password = GeneratePassword();
            ExportPrincipalToFile(serverUrl, principal, password);

            Logger.LogInformation("Creating login '{Login}' on master database of '{ServerUrl}'.", principal, serverUrl);
            var masterConnectionString = BuildConnectionString(serverUrl, MasterDatabaseName, adminUser, adminPassword);
            await CreateLogin(masterConnectionString, principal, password);

            Logger.LogInformation("Creating user '{Username}' on database '{Database}' of '{ServerUrl}'.", principal, databaseName, serverUrl);
            var databaseConnectionString = BuildConnectionString(serverUrl, databaseName, adminUser, adminPassword);
            var sid = await CreateUser(databaseConnectionString, principal, principal, role);

            return new SqlUserInfo(principal, principal, sid, password);
        }

        private async Task<SqlUserInfo> CreateLoginOnSecondary(string serverUrl, string databaseName, string adminUser, string adminPassword, SqlUserInfo primaryInfo)
        {
            var principal = primaryInfo.Username;
            var password = GeneratePassword();
            ExportPrincipalToFile(serverUrl, principal, password);
            var sid = primaryInfo.UserSid;

            Logger.LogInformation("Creating login '{Login}' on master database of '{ServerUrl}'.", principal, databaseName, serverUrl);
            var databaseConnectionString = BuildConnectionString(serverUrl, MasterDatabaseName, adminUser, adminPassword);
            await CreateLogin(databaseConnectionString, principal, password, sid);

            return new SqlUserInfo(principal, principal, sid, password);
        }

        private async Task CreateLogin(string connectionString, string login, string password, SqlBytes sid = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                Logger.LogInformation("Checking if login '{Login}' exists.", login);
                var getLoginWithNameCommand = new SqlCommand(SqlQueries.GetLoginWithName, connection);
                getLoginWithNameCommand.Parameters.AddWithValue(SqlQueries.ParameterLogin, login);
                if (await DoesCommandReturnsRows(getLoginWithNameCommand))
                {
                    throw new ArgumentException($"There is already a login named {login}!", nameof(login));
                }

                Logger.LogInformation("Login '{Login}' does not exist.", login);
                Logger.LogInformation("Creating login '{Login}'.", login);
                if (!_whatIf)
                {
                    var commandText = sid == null ? 
                        SqlQueries.CreateLogin : 
                        SqlQueries.CreateLoginWithSid.Replace(SqlQueries.ParameterSid, ConvertSqlBytesToHexString(sid));

                    commandText = commandText
                        .Replace(SqlQueries.ParameterLogin, login)
                        .Replace(SqlQueries.ParameterPassword, $"'{password}'");

                    var createLoginCommand = new SqlCommand(commandText, connection);

                    await createLoginCommand.ExecuteNonQueryAsync();

                    Logger.LogInformation("Created login '{Login}' successfully.", login);
                }
                else
                {
                    Logger.LogInformation("WhatIf mode is on: login '{Login}' was not created.", login);
                }
            }
        }

        private async Task<SqlBytes> CreateUser(string connectionString, string username, string login, string role)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                Logger.LogInformation("Checking if user '{Username}' exists.", username);
                var getUserWithNameCommand = new SqlCommand(SqlQueries.GetUserWithName, connection);
                getUserWithNameCommand.Parameters.AddWithValue(SqlQueries.ParameterUsername, username);
                if (await DoesCommandReturnsRows(getUserWithNameCommand))
                {
                    throw new ArgumentException($"There is already a user named {username}!", nameof(username));
                }

                Logger.LogInformation("User '{Username}' does not exist.", username);
                Logger.LogInformation("Creating user '{Username}'.", username);
                if (!_whatIf)
                {
                    var commandText = SqlQueries.CreateUser;

                    commandText = commandText
                        .Replace(SqlQueries.ParameterUsername, username)
                        .Replace(SqlQueries.ParameterLogin, login);

                    var createUserCommand = new SqlCommand(commandText, connection);

                    await createUserCommand.ExecuteNonQueryAsync();
                    Logger.LogInformation("Created user '{Username}' successfully.", username);

                    Logger.LogInformation("Granting role '{Role}' to user '{Username}'", role, username);
                    var grantRoleToUserCommand = new SqlCommand(SqlQueries.GrantRoleToUser, connection);
                    grantRoleToUserCommand.Parameters.AddWithValue(SqlQueries.ParameterUsername, username);
                    grantRoleToUserCommand.Parameters.AddWithValue(SqlQueries.ParameterRole, role);

                    await grantRoleToUserCommand.ExecuteNonQueryAsync();
                    Logger.LogInformation("Granted role '{Role}' to user '{Username}' successfully.", role, username);

                    using (var reader = await getUserWithNameCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetSqlBytes(1);
                        }
                        else
                        {
                            throw new ArgumentException("Created user successfully but could not get user's SID!");
                        }
                    }
                }
                else
                {
                    Logger.LogInformation("WhatIf mode is on: user '{Username}' was not created.", username);
                    return null;
                }
            }
        }

        private async Task CheckConnection(string serverUrl, string databaseName, string username, string password)
        {
            Logger.LogInformation("Testing connection of '{Username}' on database {DatabaseName} of '{ServerUrl}'.", username, databaseName, serverUrl);
            var connectionString = BuildConnectionString(serverUrl, databaseName, username, password);
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
            }
        }

        private static string BuildConnectionString(string serverUrl, string databaseName, string username, string password)
        {
            var builder = new SqlConnectionStringBuilder
            {
                ["Server"] = serverUrl
            };

            builder.InitialCatalog = databaseName;
            builder.UserID = username;
            builder.Password = password;

            return builder.ConnectionString;
        }

        private string GeneratePrincipalName(string roleName)
        {
            var currentDate = DateTime.Now;
            return $"{_newPrincipalPrefix}{roleName}x{currentDate.Year}{currentDate.Month}{currentDate.Day}";
        }

        private static string GeneratePassword()
        {
            return Membership.GeneratePassword(PasswordLength, PasswordNumNonAlphanumericCharacters);
        }

        private static void ExportPrincipalToFile(string serverUrl, string principal, string password)
        {
            File.WriteAllText($"{principal}_{serverUrl}.txt", $"Username: {principal}\r\nPassword: {password}");
        }

        private static async Task<bool> DoesCommandReturnsRows(SqlCommand command)
        {
            using (var reader = await command.ExecuteReaderAsync())
            {
                return reader.HasRows;
            }
        }

        private static string ConvertSqlBytesToHexString(SqlBytes bytes)
        {
            return "0x" + BitConverter.ToString(bytes.Buffer).Replace("-", "");
        }
    }
}
