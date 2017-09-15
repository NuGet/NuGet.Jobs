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

        private static ILogger logger = null;

        static int Main(string[] args)
        {
            var services = new ServiceCollection();
            ValidationConfiguration configuration;
            try
            {
                configuration = ConfigureWithCommandLine(services, args);
            }
            catch(Exception e)
            {
                if (logger != null)
                {
                    logger.LogError(Error.ConfigurationReadFailure, e, "Failed to read configuration");
                }
                else
                {
                    Console.Error.WriteLine("Failed to read configuration: {0}", e);
                }
                PrintUsage();
                return 1;
            }
            ConfigureServices(services, configuration);
            var serviceProvider = CreateProvider(services);

            try
            {
                ConfigurationValidator.Validate(configuration);
            }
            catch(Exception e)
            {
                logger.LogError(Error.ConfigurationValidationFailure, e, "Failed to validate configuration");
                return 1;
            }

            logger.LogInformation("Configuration validated successfully");

            var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();
            if (configurationRoot[ValidateOnlyConfigurationKey] == "true")
            {
                return 0;
            }

            logger.LogInformation("Starting up the orchestration");

            return 0;
        }

        private static ValidationConfiguration ConfigureWithCommandLine(IServiceCollection services, string[] args)
        {
            var loggerFactoryBootstrapper = new LoggerFactoryBootstrapper();
            logger = loggerFactoryBootstrapper.LoggerFactory.CreateLogger(LoggingCategory);
            logger.LogInformation("Reading configuration");

            var configurationRoot = GetConfigurationRoot(args, loggerFactoryBootstrapper);

            if (loggerFactoryBootstrapper.TryInitializeApplicationInsights(configurationRoot[JobArgumentNames.InstrumentationKey]))
            {
                logger = loggerFactoryBootstrapper.LoggerFactory.CreateLogger(LoggingCategory);
            }

            services.AddSingleton(loggerFactoryBootstrapper.LoggerFactory);
            services.AddLogging();

            var configuration = new ValidationConfiguration();
            configurationRoot.GetSection(ConfigurationSectionName).Bind(configuration);

            services.AddSingleton(configuration);
            services.AddSingleton(configurationRoot);
            return configuration;
        }

        private static IConfigurationRoot GetConfigurationRoot(string[] args, LoggerFactoryBootstrapper loggerFactoryBootstrapper)
        {
            bool validateOnly = args.Any(arg => arg == "-" + ValidateArgument);
            if (validateOnly)
            {
                args = args.Where(arg => arg != "-" + ValidateArgument).ToArray();
            }

            IDictionary<string, string> argsDictionary = GetCommandLineArguments(args, loggerFactoryBootstrapper.LoggerFactory);
            string configurationFilename = JobConfigurationManager.GetArgument(argsDictionary, ConfigurationArgument);

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

        private static void ConfigureServices(IServiceCollection services, ValidationConfiguration configuration)
        {
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
