// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Sql;

namespace NuGet.Jobs
{
    /// <summary>
    /// Used for diagnostics when a connection isn't yet established.
    /// Consider removing once ConnectionStringBuilder property is exposed on AzureSqlConnectionFactory.
    /// </summary>
    public class DatabaseIdentifier
    {
        public DatabaseIdentifier(ISqlConnectionFactory factory)
        {
            DataSource = factory.DataSource;
            InitialCatalog = factory.InitialCatalog;
        }

        public string DataSource { get; }

        public string InitialCatalog { get; }
    }
}
