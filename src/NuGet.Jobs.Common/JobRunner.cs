﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;

namespace NuGet.Jobs
{
    public static class JobRunner
    {
        public static IServiceContainer ServiceContainer;

        private static ILogger _logger;
        
        private const string JobSucceeded = "Job Succeeded";
        private const string JobUninitialized = "Job Failed to Initialize";
        private const string JobFailed = "Job Failed to Run";

        static JobRunner()
        {
            ServiceContainer = new ServiceContainer();
            ServiceContainer.AddService(typeof(ISecretReaderFactory), new SecretReaderFactory());
        }

        /// <summary>
        /// This is a static method to run a job whose args are passed in
        /// By default,
        ///     a) The job will be run continuously in a while loop. Could be overridden using 'once' argument
        ///     b) The sleep duration between each run when running continuously is 5000 milliSeconds. Could be overridden using '-Sleep' argument
        /// </summary>
        /// <param name="job">Job to run</param>
        /// <param name="commandLineArgs">Args contains args to the job runner like (dbg, once and so on) and for the job itself</param>
        /// <returns></returns>
        public static async Task Run(JobBase job, string[] commandLineArgs)
        {
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-" + JobArgumentNames.Dbg, StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            // Configure logging before Application Insights is enabled.
            // This is done so, in case Application Insights fails to initialize, we still see output.
            var loggerFactory = ConfigureLogging(job);

            try
            {
                _logger.LogInformation("Started...");

                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>

                var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(ServiceContainer, loggerFactory.CreateLogger(typeof(JobConfigurationManager)), commandLineArgs, job.JobName);

                // Setup logging
                if (!ApplicationInsights.Initialized)
                {
                    string instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                    if (!string.IsNullOrWhiteSpace(instrumentationKey))
                    {
                        ApplicationInsights.Initialize(instrumentationKey);
                    }
                }

                // Configure our logging again with Application Insights initialized.
                loggerFactory = ConfigureLogging(job);

                var runContinuously = !JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once);
                var reinitializeAfterSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.ReinitializeAfterSeconds);
                var sleepDuration = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Sleep); // sleep is in milliseconds

                if (!sleepDuration.HasValue)
                {
                    sleepDuration = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Interval);
                    if (sleepDuration.HasValue)
                    {
                        sleepDuration = sleepDuration.Value * 1000; // interval is in seconds
                    }
                }

                if (sleepDuration == null)
                {
                    _logger.LogInformation("SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                    sleepDuration = 5000;
                }

                if (!reinitializeAfterSeconds.HasValue)
                {
                    _logger.LogInformation(
                        $"{JobArgumentNames.ReinitializeAfterSeconds} command line argument is not provided or is not a valid integer. " +
                        "The job will reinitialize on every iteration");
                }

                // Ensure that SSLv3 is disabled and that Tls v1.2 is enabled.
                ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // Run the job loop
                await JobLoop(job, runContinuously, sleepDuration.Value, reinitializeAfterSeconds, jobArgsDictionary);
            }
            catch (Exception ex)
            {
                _logger.LogError("Job runner threw an exception: {Exception}", ex);
            }

            Trace.Close();
            TelemetryConfiguration.Active.TelemetryChannel.Flush();
        }

        private static ILoggerFactory ConfigureLogging(JobBase job)
        {
            var loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
            var logger = loggerFactory.CreateLogger(job.GetType());

            job.SetLogger(loggerFactory, logger);
            _logger = loggerFactory.CreateLogger(typeof(JobRunner));

            return loggerFactory;
        }

        private static string PrettyPrintTime(double milliSeconds)
        {
            var seconds = (milliSeconds/1000.0);
            var minutes = (milliSeconds/60000.0);
            return
                $"'{milliSeconds:F3}' ms (or '{seconds:F3}' seconds or '{minutes:F3}' mins)";
        }

        private static async Task JobLoop(
            JobBase job,
            bool runContinuously,
            int sleepDuration,
            int? reinitializeAfterSeconds,
            IDictionary<string, string> jobArgsDictionary)
        {
            // Run the job now
            var stopWatch = new Stopwatch();
            Stopwatch timeSinceInitialization = null;

            while (true)
            {
                _logger.LogInformation("Running {RunType}", (runContinuously ? " continuously..." : " once..."));
                _logger.LogInformation("SleepDuration is {SleepDuration}", PrettyPrintTime(sleepDuration));
                _logger.LogInformation("Job run started...");

                var initialized = false;
                stopWatch.Restart();

                try
                {
                    if (ShouldInitialize(reinitializeAfterSeconds, timeSinceInitialization))
                    {
                        job.Init(ServiceContainer, jobArgsDictionary);
                        timeSinceInitialization = Stopwatch.StartNew();
                    }

                    initialized = true;

                    await job.Run();

                    _logger.LogInformation(JobSucceeded);
                }
                catch (Exception e)
                {
                    _logger.LogError("{JobState}: {Exception}", initialized ? JobFailed : JobUninitialized, e);
                }
                finally
                {
                    _logger.LogInformation("Job run ended...");
                    stopWatch.Stop();
                    _logger.LogInformation("Job run took {RunDuration}", PrettyPrintTime(stopWatch.ElapsedMilliseconds));
                }

                if (!runContinuously)
                {
                    // It is ok that we do not flush the logs here.
                    // Logs will be flushed at the end of Run().
                    break;
                }

                // Wait for <sleepDuration> milliSeconds and run the job again
                _logger.LogInformation("Will sleep for {SleepDuration} before the next Job run", PrettyPrintTime(sleepDuration));
                
                await Task.Delay(sleepDuration);
            }
        }

        private static bool ShouldInitialize(int? reinitializeAfterSeconds, Stopwatch timeSinceInitialization)
        {
            // If there is no wait time between reinitializations, always reinitialize.
            if (!reinitializeAfterSeconds.HasValue)
            {
                return true;
            }

            // A null time since last initialization indicates that the job hasn't been initialized yet.
            if (timeSinceInitialization == null)
            {
                return true;
            }

            // Otherwise, only reinitialize if the reinitialization threshold has been reached.
            return (timeSinceInitialization.Elapsed.TotalSeconds > reinitializeAfterSeconds.Value);
        }
    }
}
