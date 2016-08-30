﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.KeyVault;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    public static class JobRunner
    {
        public static IServiceContainer ServiceContainer;

        private const string _jobSucceeded = "Job Succeeded";
        private const string _jobFailed = "Job Failed";

        private const int DefaultSleepDuration = 5000;

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
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            bool consoleLogOnly = false;
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-ConsoleLogOnly", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                consoleLogOnly = true;
            }

            // Set the default trace listener, so if we get args parsing issues they will be printed. This will be overriden by the configured trace listener
            // after config is parsed.
            job.SetJobTraceListener(new JobTraceListener());

            try
            {
                Trace.TraceInformation("Started...");

                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(commandLineArgs, job.JobName, (ISecretReaderFactory)ServiceContainer.GetService(typeof(ISecretReaderFactory)));

                // Set JobTraceListener. This will be done on every job run as well
                await SetJobTraceListener(job, consoleLogOnly, jobArgsDictionary);

                bool runContinuously = !await jobArgsDictionary.GetOrDefault<bool>(JobArgumentNames.Once);

                var sleepDurationNotFound = -1;
                int sleepDuration = await jobArgsDictionary.GetOrDefault<int>(JobArgumentNames.Sleep, sleepDurationNotFound); // sleep is in milliseconds
                if (sleepDuration == sleepDurationNotFound)
                {
                    sleepDuration = await jobArgsDictionary.GetOrDefault<int>(JobArgumentNames.Interval, sleepDurationNotFound);
                    if (sleepDuration != sleepDurationNotFound)
                    {
                        sleepDuration = sleepDuration * 1000; // interval is in seconds
                    }
                    else
                    {
                        Trace.TraceWarning("SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                        sleepDuration = DefaultSleepDuration;
                    }
                }

                // Setup the job for running
                await JobSetup(job, consoleLogOnly, jobArgsDictionary);

                // Run the job loop
                await JobLoop(job, runContinuously, sleepDuration, consoleLogOnly, jobArgsDictionary);
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions.Count > 0 ? ex.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    Trace.TraceError("[FAILED]: " + innerEx);
                }
                else
                {
                    Trace.TraceError("[FAILED]: " + ex);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FAILED]: " + ex);
            }

            // Call FlushAll here. This is VERY IMPORTANT
            // Exception message(s) when the job faults are still in the queue. Need to be flushed
            // Also, when the job is only run once, FlushAll is important again
            job.JobTraceListener.Close();
        }

        private static string PrettyPrintTime(double milliSeconds)
        {
            const string precisionSpecifier = "F3";
            return string.Format("'{0}' ms (or '{1}' seconds or '{2}' mins)",
                milliSeconds.ToString(precisionSpecifier),  // Time in milliSeconds
                (milliSeconds / 1000.0).ToString(precisionSpecifier),  // Time in seconds
                (milliSeconds / 60000.0).ToString(precisionSpecifier));  // Time in minutes
        }

        private static async Task SetJobTraceListener(JobBase job, bool consoleLogOnly, IArgumentsDictionary argsDictionary)
        {
            if (consoleLogOnly)
            {
                job.SetJobTraceListener(new JobTraceListener());
            }
            else
            {
                var connectionString = await argsDictionary.GetOrThrow<string>(JobArgumentNames.LogsAzureStorageConnectionString);
                job.SetJobTraceListener(new AzureBlobJobTraceListener(job.JobName, connectionString));
            }
        }

        private static async Task JobSetup(JobBase job, bool consoleLogOnly, IArgumentsDictionary jobArgsDictionary)
        {
            if (await jobArgsDictionary.GetOrDefault<bool>("dbg"))
            {
                throw new ArgumentException("-dbg is a special argument and should be the first argument...");
            }

            if (await jobArgsDictionary.GetOrDefault<bool>("ConsoleLogOnly"))
            {
                throw new ArgumentException("-ConsoleLogOnly is a special argument and should be the first argument (can be the second if '-dbg' is used)...");
            }

            job.ConsoleLogOnly = consoleLogOnly;
        }

        private static async Task<string> JobLoop(JobBase job, bool runContinuously, int sleepDuration, bool consoleLogOnly, IArgumentsDictionary jobArgsDictionary)
        {
            // Run the job now
            var stopWatch = new Stopwatch();
            bool initialized;
            bool success;

            while (true)
            {
                Trace.WriteLine("Running " + (runContinuously ? " continuously..." : " once..."));
                Trace.WriteLine("SleepDuration is " + PrettyPrintTime(sleepDuration));
                Trace.TraceInformation("Job run started...");

                // Force a flush here to create a blob corresponding to run indicating that the run has started
                job.JobTraceListener.Flush();

                stopWatch.Restart();
                initialized = await job.Init(jobArgsDictionary);
                success = initialized && await job.Run();
                stopWatch.Stop();

                Trace.WriteLine("Job run ended...");
                Trace.TraceInformation("Job run took {0}", PrettyPrintTime(stopWatch.ElapsedMilliseconds));
                if (success)
                {
                    Trace.TraceInformation(_jobSucceeded);
                }
                else if (!initialized)
                {
                    Trace.TraceWarning("The job could not be initialized successfully with the arguments passed");
                }
                else
                {
                    Trace.TraceWarning(_jobFailed);
                }

                // At this point, FlushAll is not called, So, what happens when the job is run only once?
                // Since, FlushAll is called right at the end of the program, this is no issue
                if (!runContinuously)
                {
                    break;
                }

                // Wait for <sleepDuration> milliSeconds and run the job again
                Trace.TraceInformation("Will sleep for {0} before the next Job run", PrettyPrintTime(sleepDuration));

                // Flush All the logs for this run
                job.JobTraceListener.Close();

                // Use Console.WriteLine when you don't want it to be logged in Azure blobs
                Console.WriteLine("Sleeping for {0} before the next job run", PrettyPrintTime(sleepDuration));
                Thread.Sleep(sleepDuration);

                await SetJobTraceListener(job, consoleLogOnly, jobArgsDictionary);
            }

            return success ? _jobSucceeded : _jobFailed;
        }
    }
}
