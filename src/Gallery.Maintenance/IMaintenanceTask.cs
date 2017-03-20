// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Gallery.Maintenance
{
    public interface IMaintenanceTask
    {
        Task<bool> RunAsync(Job job);
    }
}
