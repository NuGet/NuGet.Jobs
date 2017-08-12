// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Runs all <see cref="IMaintenanceTask"/>s against the Gallery database.
    /// </summary>
    public class Job : JobBase
    {
        private static readonly Lazy<IEnumerable<IMaintenanceTask>> _tasks = new Lazy<IEnumerable<IMaintenanceTask>>(GetMaintenanceTasks);

        public SqlConnectionStringBuilder GalleryDatabase { get; private set; }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryDatabase);
            GalleryDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
        }

        public override async Task Run()
        {
            var failedTasks = new List<string>();

            foreach (var task in _tasks.Value)
            {
                var taskName = task.GetType().Name;

                try
                {
                    Logger.LogInformation("Running task '{taskName}'...", taskName);

                    await task.RunAsync(this);

                    Logger.LogInformation("Finished task '{taskName}'.", taskName);
                }
                catch (Exception exception)
                {
                    Logger.LogError("Task '{taskName}' failed: {Exception}", taskName, exception);
                    failedTasks.Add(taskName);
                }
            }
            
            if (failedTasks.Any())
            {
                throw new Exception($"{failedTasks.Count()} tasks failed: {string.Join(", ", failedTasks)}");
            }
        }

        private static IEnumerable<IMaintenanceTask> GetMaintenanceTasks()
        {
            var taskBaseType = typeof(IMaintenanceTask);

            return taskBaseType.Assembly.GetTypes()
                .Where(type => type.IsClass && taskBaseType.IsAssignableFrom(type))
                .Select(type => (IMaintenanceTask)Activator.CreateInstance(type));
        }
    }
}
