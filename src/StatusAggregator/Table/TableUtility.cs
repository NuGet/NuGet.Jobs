// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;

namespace StatusAggregator.Table
{
    public static class TableUtility
    {
        /// <summary>
        /// The <see cref="ITableEntity.ETag"/> to provide when the existing content in the table is unimportant.
        /// E.g. "if match any".
        /// </summary>
        public const string ETagWildcard = "*";

        private static readonly IDictionary<Type, string> PartitionKeyMap = new Dictionary<Type, string>
        {
            { typeof(CursorEntity), CursorEntity.DefaultPartitionKey },
            { typeof(IncidentEntity), IncidentEntity.DefaultPartitionKey },
            { typeof(IncidentGroupEntity), IncidentGroupEntity.DefaultPartitionKey },
            { typeof(EventEntity), EventEntity.DefaultPartitionKey },
            { typeof(MessageEntity), MessageEntity.DefaultPartitionKey },
            { typeof(ManualStatusChangeEntity), ManualStatusChangeEntity.DefaultPartitionKey }
        };

        public static string GetPartitionKey<T>()
        {
            if (PartitionKeyMap.TryGetValue(typeof(T), out var partitionkey))
            {
                return partitionkey;
            }

            throw new ArgumentException(nameof(T));
        }
    }
}
