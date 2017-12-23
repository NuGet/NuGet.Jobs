// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace CreateNewSqlUsers
{
    public static class SqlQueries
    {
        public const string ParameterUsername = "@Username";
        public const string ParameterLogin = "@Login";
        public const string ParameterPassword = "@Password";
        public const string ParameterSid = "@Sid";
        public const string ParameterRole = "@Role";

        public static string GetLoginWithName = $"SELECT name, sid FROM sys.sql_logins WHERE name = {ParameterLogin}";
        public static string CreateLogin = $"CREATE LOGIN {ParameterLogin} WITH PASSWORD = {ParameterPassword}";
        public static string CreateLoginWithSid = $"{CreateLogin}, SID = {ParameterSid}";

        public static string GetUserWithName = $"SELECT name, sid FROM sys.database_principals WHERE name = {ParameterUsername} AND type_desc = 'SQL_USER'";
        public static string CreateUser = $"CREATE USER {ParameterUsername} FROM LOGIN {ParameterLogin}";
        public static string GrantRoleToUser = $"EXEC sp_addrolemember {ParameterRole}, {ParameterUsername}";
    }
}
