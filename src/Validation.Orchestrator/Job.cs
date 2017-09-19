﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Configuration;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string ValidateOnlyConfigurationKey = nameof(ValidateOnlyConfiguration.ValidateOnly);

        private string _configurationFilename;
        private bool _validateOnly;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Indicates whether we had successful configuration validation or not
        /// </summary>
        public bool ConfigurationValidated { get; set; }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            _configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _validateOnly = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, ValidateArgument, defaultValue: false);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(_configurationFilename, _validateOnly));
            ConfigurationValidated = false;
        }

        public override Task Run()
        {
            var validator = GetRequiredService<ConfigurationValidator>();
            validator.Validate();
            ConfigurationValidated = true;

            return Task.FromResult(0);
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename)
                .AddInMemoryCollection(new[] { new KeyValuePair<string, string>(ValidateOnlyConfigurationKey, validateOnly.ToString()) });

            var unprocessedConfiguration = builder.Build();

            if (_validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return unprocessedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(unprocessedConfiguration);
            return new SecretConfigurationReader(unprocessedConfiguration, secretReaderFactory);
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();
            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            services.AddOptions();
            services.AddSingleton(this.LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<ValidateOnlyConfiguration>(configurationRoot);
            services.AddTransient<ConfigurationValidator>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
