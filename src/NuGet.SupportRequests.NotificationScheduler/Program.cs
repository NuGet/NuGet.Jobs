// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using NuGet.SupportRequests.NotificationScheduler.Models;
using NuGet.SupportRequests.NotificationScheduler.Services;
using NuGet.SupportRequests.NotificationScheduler.Tasks;

namespace NuGet.SupportRequests.NotificationScheduler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Set the default trace listener, so if we get args parsing issues they will be printed. This will be overriden by the configured trace listener
            // after config is parsed.
            var traceListener = new JobTraceListener();
            Trace.Listeners.Add(traceListener);

            Trace.TraceInformation("Started...");

            try
            {
                var stopWatch = new Stopwatch();
                var argsDictionary = ParseArgsDictionary(args);

                if (!argsDictionary.ContainsKey(JobArgumentNames.ScheduledTask))
                {
                    throw new NotSupportedException("Unknown scheduled task. The argument -Task appears to be missing.");
                }

                Trace.TraceInformation("Job run started...");

                // Force a flush here to create a blob corresponding to run indicating that the run has started
                traceListener.Flush();

                stopWatch.Restart();

                var instrumentationKey = argsDictionary.GetOrDefault<string>(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerFactory = LoggingSetup.CreateLoggerFactory();
                var scheduledTaskName = argsDictionary[JobArgumentNames.ScheduledTask];

                IScheduledTask scheduledTask = null;
                if (IsTaskOfType<OnCallDailyNotificationTask>(scheduledTaskName))
                {
                    var supportRequestService = CreateSupportRequestsService(argsDictionary, loggerFactory);
                    var messagingService = CreateMessagingService(argsDictionary, loggerFactory);
                    scheduledTask = new OnCallDailyNotificationTask(loggerFactory, supportRequestService, messagingService);
                }
                else if (IsTaskOfType<WeeklySummaryNotificationTask>(scheduledTaskName))
                {
                    var supportRequestService = CreateSupportRequestsService(argsDictionary, loggerFactory);
                    var messagingService = CreateMessagingService(argsDictionary, loggerFactory);
                    scheduledTask = new WeeklySummaryNotificationTask(loggerFactory, supportRequestService, messagingService);
                }

                if (scheduledTask != null)
                {
                    scheduledTask.Run().Wait();
                }
                else
                {
                    throw new NotSupportedException($"Unknown scheduled task: '{scheduledTaskName}'.");
                }

                stopWatch.Stop();

                Trace.WriteLine("Job run ended...");
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }

            // Flush here. This is VERY IMPORTANT!
            // Exception messages from when the job faults are still in the queue and need to be flushed.
            // Also, if the job is only run once, this is what flushes the queue.
            traceListener.Close();
        }

        private static bool IsTaskOfType<T>(string scheduledTaskName)
        {
            return string.Equals(
              typeof(T).Name.Replace("Task", string.Empty),
              scheduledTaskName,
              StringComparison.OrdinalIgnoreCase);
        }

        private static SupportRequestService CreateSupportRequestsService(
          IDictionary<string, string> argsDictionary,
          ILoggerFactory loggerFactory)
        {
            var databaseConnectionString = argsDictionary[JobArgumentNames.SourceDatabase];
            var sourceDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
            var supportRequestRepository = new SupportRequestRepository(loggerFactory, sourceDatabase);

            var pagerDutyConfiguration = new PagerDutyConfiguration(
              argsDictionary[JobArgumentNames.PagerDutyAccountName],
              argsDictionary[JobArgumentNames.PagerDutyApiKey]
              );

            return new SupportRequestService(supportRequestRepository, pagerDutyConfiguration);
        }

        private static MessagingService CreateMessagingService(
          IDictionary<string, string> argsDictionary,
          ILoggerFactory loggerFactory)
        {
            var targetEmailAddress = argsDictionary[JobArgumentNames.TargetEmailAddress];
            var smtpUri = argsDictionary[JobArgumentNames.SmtpUri];

            return new MessagingService(loggerFactory, smtpUri, targetEmailAddress);
        }

        private static void HandleException(Exception exception)
        {
            var aggregateException = exception as AggregateException;
            if (aggregateException != null)
            {
                var innerEx = aggregateException.InnerExceptions.Count > 0 ? aggregateException.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    Trace.TraceError("[FAILED]: " + innerEx);
                }
                else
                {
                    Trace.TraceError("[FAILED]: " + aggregateException);
                }
            }
            else
            {
                Trace.TraceError("[FAILED]: " + exception);
            }
        }

        private static IDictionary<string, string> ParseArgsDictionary(string[] commandLineArgs)
        {
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
            var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(
                commandLineArgs,
                "SupportRequests.NotificationScheduler",
                new SecretReaderFactory());

            return jobArgsDictionary;
        }
    }
}
