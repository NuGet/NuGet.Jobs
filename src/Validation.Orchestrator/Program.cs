// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using Serilog;

namespace NuGet.Services.Validation.Orchestrator
{
    class Program
    {
        static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            ConfigureWithCommandLine(services, args);

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("settings.json");

            var nonInjectedConfiguration = builder.Build();
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(nonInjectedConfiguration);
            var configurationRoot = new SecretConfigurationReader(nonInjectedConfiguration, secretReaderFactory);

            var configuration = new Configuration();
            var configurationSection = configurationRoot.GetSection("Configuration");
            configurationSection.Bind(configuration);

            ConfigurationValidator.Validate(configuration);
        }

        private static void ConfigureWithCommandLine(IServiceCollection services, string[] args)
        {
            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(false);
            loggerConfiguration.WriteTo.Console();

            //services.AddTransient<ISecretReaderFactory>();

            var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);

            JobConfigurationManager.GetJobArgsDictionary(loggerFactory.CreateLogger("Validation"), args, "Validation.Orchestrator", );

            throw new NotImplementedException();
        }


    }
}
