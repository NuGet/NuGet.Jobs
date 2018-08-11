// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public interface IManualStatusChangeHandler
    {
        Task Handle(ITableWrapper table, ManualStatusChangeEntity entity);
    }

    public interface IManualStatusChangeHandler<T>
        where T : ManualStatusChangeEntity
    {
        Task Handle(T entity);
    }
}
