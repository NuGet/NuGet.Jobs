// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;

namespace NuGet.Jobs
{
    public abstract class JobBase
    {
        private readonly EventSource _jobEventSource;

        private Dictionary<string, ISqlConnectionFactory> _sqlConnectionFactories;

        protected JobBase()
            : this(null)
        {
            _sqlConnectionFactories = new Dictionary<string, ISqlConnectionFactory>();
        }

        protected JobBase(EventSource jobEventSource)
        {
            JobName = GetType().ToString();
            _jobEventSource = jobEventSource;
        }

        public string JobName { get; private set; }

        protected ILoggerFactory LoggerFactory { get; private set; }

        protected ILogger Logger { get; private set; }

        public void SetLogger(ILoggerFactory loggerFactory, ILogger logger)
        {
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase<T>(IServiceProvider serviceProvider)
            where T : IDbConfiguration
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var secretInjector = serviceProvider.GetRequiredService<ISecretInjector>();
            var connectionString = serviceProvider.GetRequiredService<IOptionsSnapshot<T>>().Value.ConnectionString;
            var connectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector);
            
            return RegisterDatabase(nameof(T), connectionString, secretInjector);
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by non-validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary, string argName)
        {
            if (serviceContainer == null)
            {
                throw new ArgumentNullException(nameof(serviceContainer));
            }

            if (jobArgsDictionary == null)
            {
                throw new ArgumentNullException(nameof(jobArgsDictionary));
            }

            if (string.IsNullOrEmpty(argName))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(argName));
            }

            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));
            var connectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, argName);

            return RegisterDatabase(argName, connectionString, secretInjector);
        }

        /// <summary>
        /// Register a job database at initialization time. Each call should overwrite any existing
        /// registration because <see cref="JobRunner"/> calls <see cref="Init"/> on every iteration.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        private SqlConnectionStringBuilder RegisterDatabase(string name, string connectionString, ISecretInjector secretInjector)
        {
            var connectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector, Logger);
            _sqlConnectionFactories[name] = connectionFactory;

            return connectionFactory.SqlConnectionStringBuilder;
        }

        /// <summary>
        /// Create a SqlConnection, for use by validation jobs.
        /// </summary>
        public Task<SqlConnection> CreateSqlConnectionAsync<T>()
            where T : IDbConfiguration
        {
            var name = nameof(T);
            if (!_sqlConnectionFactories.ContainsKey(name))
            {
                throw new InvalidOperationException($"Database {name} has not been registered.");
            }

            return _sqlConnectionFactories[name].CreateAsync();
        }

        /// <summary>
        /// Creates and opens a SqlConnection, for use by non-validation jobs.
        /// </summary>
        public Task<SqlConnection> OpenSqlConnectionAsync(string argName)
        {
            if (string.IsNullOrEmpty(argName))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(argName));
            }

            if (!_sqlConnectionFactories.ContainsKey(argName))
            {
                throw new InvalidOperationException($"Database {argName} has not been registered.");
            }

            return _sqlConnectionFactories[argName].OpenAsync();
        }

        public abstract void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary);

        public abstract Task Run();
    }
}
