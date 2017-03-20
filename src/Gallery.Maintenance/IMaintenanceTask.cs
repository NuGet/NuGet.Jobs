// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Gallery.Maintenance
{
    /// <summary>
    /// A task to be run as a part of Gallery maintenance. Makes SQL queries against the Gallery database.
    /// </summary>
    public interface IMaintenanceTask
    {
        Task<bool> RunAsync(Job job);
    }
}
