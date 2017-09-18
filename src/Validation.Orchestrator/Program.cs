// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Configuration;

namespace NuGet.Services.Validation.Orchestrator
{
    class Program
    {
        private const string LoggingCategory = "Validation.Orchestrator";

        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string ValidateOnlyConfigurationKey = "ValidateOnly";

        private static ILogger _logger = null;

        static int Main(string[] args)
        {
            IServiceProvider serviceProvider;
            if ( !TryConfigure(args, out serviceProvider) )
            {
                return 1;
            }

            _logger.LogInformation("Configuration validated successfully");

            var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();
            if (configurationRoot[ValidateOnlyConfigurationKey] == "true")
            {
                _logger.LogInformation("Validation only was requested. Terminating.");
                return 0;
            }

            _logger.LogInformation("Starting up the orchestration");

            return 0;
        }

        private static bool TryConfigure(string[] args, out IServiceProvider serviceProvider)
        {
            serviceProvider = null;
            var services = new ServiceCollection();
            try
            {
                ConfigureWithCommandLine(services, args);
            }
            catch (Exception e)
            {
                if (_logger != null)
                {
                    _logger.LogError(Error.ConfigurationReadFailure, e, "Failed to read configuration");
                }
                else
                {
                    Console.Error.WriteLine("Failed to read configuration: {0}", e);
                }
                PrintUsage();
                return false;
            }
            ConfigureServices(services);
            serviceProvider = CreateProvider(services);

            try
            {
                var validator = serviceProvider.GetRequiredService<ConfigurationValidator>();
                validator.Validate();
            }
            catch (Exception e)
            {
                _logger.LogError(Error.ConfigurationValidationFailure, e, "Failed to validate configuration");
                return false;
            }

            return true;
        }

        private static void ConfigureWithCommandLine(IServiceCollection services, string[] args)
        {
            var loggerFactoryBootstrapper = new LoggerFactoryBootstrapper();
            _logger = loggerFactoryBootstrapper.LoggerFactory.CreateLogger(LoggingCategory);
            _logger.LogInformation("Reading configuration");

            var configurationRoot = GetConfigurationRoot(args, loggerFactoryBootstrapper);

            if (loggerFactoryBootstrapper.TryInitializeApplicationInsights(configurationRoot[JobArgumentNames.InstrumentationKey]))
            {
                _logger = loggerFactoryBootstrapper.LoggerFactory.CreateLogger(LoggingCategory);
            }

            services.AddSingleton(loggerFactoryBootstrapper.LoggerFactory);
            services.AddLogging();

            services.AddOptions();
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));

            services.AddSingleton(configurationRoot);
        }

        private static IConfigurationRoot GetConfigurationRoot(string[] args, LoggerFactoryBootstrapper loggerFactoryBootstrapper)
        {
            bool validateOnly = args.Any(arg => arg.Equals("-" + ValidateArgument, StringComparison.OrdinalIgnoreCase));
            if (validateOnly)
            {
                args = args.Where(arg => arg != "-" + ValidateArgument).ToArray();
            }

            IDictionary<string, string> argsDictionary = GetCommandLineArguments(args, loggerFactoryBootstrapper.LoggerFactory);
            string configurationFilename = JobConfigurationManager.GetArgument(argsDictionary, ConfigurationArgument);

            _logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename);

            if (validateOnly)
            {
                builder.AddInMemoryCollection(new[] { new KeyValuePair<string, string>(ValidateOnlyConfigurationKey, "true") });
            }

            var unprocessedConfiguration = builder.Build();

            if (validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return unprocessedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(unprocessedConfiguration);
            var configurationRoot = new SecretConfigurationReader(unprocessedConfiguration, secretReaderFactory);
            return configurationRoot;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<ConfigurationValidator>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private static IDictionary<string, string> GetCommandLineArguments(string[] args, ILoggerFactory loggerFactory)
        {
            var bootstrapConfigurationBuilder = new ConfigurationBuilder();
            var bootstrapConfiguration = bootstrapConfigurationBuilder.Build();
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(bootstrapConfiguration);
            return JobConfigurationManager.GetJobArgsDictionary(loggerFactory.CreateLogger(LoggingCategory), args, LoggingCategory, secretReaderFactory);
        }

        private static void PrintUsage()
        {
            var executableName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            Console.WriteLine("Usage: {0} -{1} <filename> [-{2}]", executableName, ConfigurationArgument, ValidateArgument);
        }
    }
}
