// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    // Public only to facilitate testing.
    public sealed class VerifiedPackagesExporter : SqlExporter
    {
        private const string _colPackageId = "Id";
        private readonly string _verifiedPackagesScript;

        public VerifiedPackagesExporter(
            ILogger<SqlExporter> logger,
            string defaultConnectionString,
            CloudBlobContainer defaultDestinationContainer,
            string defaultVerifiedPackagesScript,
            string defaultName)
            : base(logger, defaultConnectionString, defaultDestinationContainer, defaultName)
        {
            _verifiedPackagesScript = defaultVerifiedPackagesScript;
        }

        protected override JContainer GetResultOfQuery(SqlConnection connection)
        {
            var rankingsTotalCommand = new SqlCommand(GetEmbeddedSqlScript(_verifiedPackagesScript), connection);
            rankingsTotalCommand.CommandType = CommandType.Text;
            rankingsTotalCommand.CommandTimeout = 60;

            return GetVerifiedPackages(rankingsTotalCommand.ExecuteReader());
        }

        public JArray GetVerifiedPackages(IDataReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var colNames = GetColMappingFromSqlDataReader(reader);
            var result = new JArray();

            while (reader.Read())
            {
                result.Add(reader.GetString(colNames[_colPackageId]));
            }

            reader.Close();

            return result;
        }
    }
}