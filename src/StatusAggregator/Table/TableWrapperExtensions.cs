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
            where T : ITableEntity, IComponentAffectingEntity, new()
        {
            return table
                .CreateQuery<T>()
                .Where(e => e.IsActive);
        }

        public static IQueryable<T> GetLinkedEntities<T>(this ITableWrapper table, ITableEntity entity)
            where T : ITableEntity, ILinkedEntity, new()
        {
            return table
                .CreateQuery<T>()
                .Where(e => e.ParentRowKey == entity.RowKey);
        }
    }
}
