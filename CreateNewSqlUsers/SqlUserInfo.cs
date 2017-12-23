// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlTypes;

namespace CreateNewSqlUsers
{
    public class SqlUserInfo
    {
        public string Login { get; }
        public string Username { get; }
        public SqlBytes UserSid { get; }
        public string Password { get; }

        public SqlUserInfo(string login, string username, SqlBytes userSid, string password)
        {
            Login = login;
            Username = username;
            UserSid = userSid;
            Password = password;
        }
    }
}
