﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public static class JobRunner
    {
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
            if (commandLineArgs.Length > 0 && String.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            job.Logger.Log(TraceLevel.Warning, "Started...");

            try
            {
                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigManager.GetJobArgsDictionary(job.Logger, commandLineArgs, job.JobName);

                if(JobConfigManager.TryGetBoolArgument(jobArgsDictionary, "-dbg"))
                {
                    job.Logger.Log(TraceLevel.Warning, "-dbg is a special argument and should only be passed in as the first argument. Ignoring here...");
                }

                bool runContinuously = !JobConfigManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once);
                job.Logger.Log(TraceLevel.Warning, "Running " + (runContinuously ? " continuously..." : " once..."));

                // Try and get the sleep duration provided, if any. Default is 5000 milliSeconds
                int? sleepDuration = JobConfigManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Sleep);
                if(sleepDuration == null)
                {
                    job.Logger.Log(TraceLevel.Warning, "SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                    sleepDuration = 5000;
                }
                job.Logger.Log(TraceLevel.Warning, "SleepDuration is {0}", sleepDuration);

                // Initialize the job once with everything needed. Logger(s) are already initialized
                if (!job.Init(jobArgsDictionary))
                {
                    // If the job could not be initialized successfully, STOP!
                    job.Logger.Log(TraceLevel.Error, "Exiting. The job could not be initialized successfully with the arguments passed");
                    return;
                }

                // Run the job now
                do
                {
                    await job.Run();
                    if (!runContinuously) break;

                    // Wait for <sleepDuration> milliSeconds and run the job again
                    job.Logger.Log(TraceLevel.Info, "Sleeping for " + TimeSpan.FromMilliseconds(sleepDuration.Value).ToString());
                    Thread.Sleep(sleepDuration.Value);
                } while (true);
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions.Count > 0 ? ex.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    job.Logger.Log(TraceLevel.Error, innerEx.ToString());
                }
                else
                {
                    job.Logger.Log(TraceLevel.Error, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                job.Logger.Log(TraceLevel.Error, ex.ToString());
            }

        }
    }
}
