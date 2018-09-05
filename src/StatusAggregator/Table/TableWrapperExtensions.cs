// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;
using System.Linq;

namespace StatusAggregator.Table
{
    public static class TableWrapperExtensions
    {
        public static IQueryable<T> GetActiveEntities<T>(this ITableWrapper table)
            where T : ComponentAffectingEntity, new()
        {
            return table
                .CreateQuery<T>()
                .Where(e => e.IsActive);
        }

        public static IQueryable<TChild> GetLinkedEntities<TChild, TParent>(this ITableWrapper table, TParent entity)
            where TChild : ITableEntity, IChildEntity<TParent>, new()
            where TParent : ITableEntity
        {
            return table
                .CreateQuery<TChild>()
                .Where(e => e.ParentRowKey == entity.RowKey);
        }
    }
}
