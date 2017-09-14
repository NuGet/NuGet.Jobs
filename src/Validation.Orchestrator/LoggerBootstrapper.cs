// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;
using Serilog;

namespace NuGet.Services.Validation.Orchestrator
{
    public class LoggerFactoryBootstrapper
    {
        public ILoggerFactory LoggerFactory { get; private set; }

        private LoggerConfiguration loggerConfiguration;

        public LoggerFactoryBootstrapper()
        {
            this.loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(false);
            loggerConfiguration.WriteTo.Console();

            LoggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
        }

        public bool TryInitializeApplicationInsights(string instrumentationKey)
        {
            if (string.IsNullOrWhiteSpace(instrumentationKey) || ApplicationInsights.Initialized)
            {
                return false;
            }

            ApplicationInsights.Initialize(instrumentationKey);
            // need to recreate logger factory to pick up the AI
            LoggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
            return true;
        }
    }
}
