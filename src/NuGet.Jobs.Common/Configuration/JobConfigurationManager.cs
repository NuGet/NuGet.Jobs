﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace NuGet.Jobs
{
    /// <summary>
    /// This class is used to retrieve and expose the known azure configuration settings
    /// from Environment Variables
    /// </summary>
    public static class JobConfigurationManager
    {
        /// <summary>
        /// Parses the string[] of <c>args</c> passed into the job into a dictionary of string, string.
        /// Expects the string[] to be set of pairs of argumentName and argumentValue, where, argumentName start with a hyphen
        /// </summary>
        /// <param name="jobTraceListener"></param>
        /// <param name="commandLineArgs">Arguments passed to the job via commandline or environment variable settings</param>
        /// <param name="jobName">Jobname to be used to infer environment variable settings</param>
        /// <returns>Returns a dictionary of arguments</returns>
        public static IDictionary<string, string> GetJobArgsDictionary(JobTraceListener jobTraceListener, string[] commandLineArgs, string jobName)
        {
            var allArgsList = commandLineArgs.ToList();
            if (allArgsList.Count == 0)
            {
                Trace.TraceInformation("No command-line arguments provided. Trying to pick up from environment variable for the job...");
            }

            var argsEnvVariable = "NUGETJOBS_ARGS_" + jobName;
            var envArgString = Environment.GetEnvironmentVariable(argsEnvVariable);
            if (string.IsNullOrEmpty(envArgString))
            {
                Trace.TraceWarning("No environment variable for the job arguments was provided");
            }
            else
            {
                allArgsList.AddRange(envArgString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            Trace.TraceInformation("Total number of arguments : " + allArgsList.Count);

            // Arguments are expected to be a set of pairs, where each pair is of the form '-<argName> <argValue>'
            // Or, in singles as a switch '-<switch>'
            var argsDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allArgsList.Count; i++)
            {
                if (!allArgsList[i].StartsWith("-"))
                {
                    throw new ArgumentException("Argument Name does not start with a hyphen ('-')");
                }

                var argName = allArgsList[i].Substring(1);
                if (string.IsNullOrEmpty(argName))
                {
                    throw new ArgumentException("Argument Name is null or empty");
                }

                var nextString = allArgsList.Count > i + 1 ? allArgsList[i + 1] : null;
                if (string.IsNullOrEmpty(nextString) || nextString.StartsWith("-"))
                {
                    // If the key already exists, don't add. This means that first added value is preferred
                    // Since command line args are added before args from environment variable, this is the desired behavior
                    if (!argsDictionary.ContainsKey(argName))
                    {
                        // nextString startWith hyphen, the current one is a switch
                        argsDictionary.Add(argName, bool.TrueString);
                    }
                }
                else
                {
                    var argValue = nextString;
                    if (string.IsNullOrEmpty(argValue))
                    {
                        throw new ArgumentException("Argument Value is null or empty");
                    }

                    // If the key already exists, don't add. This means that first added value is preferred
                    // Since command line args are added before args from environment variable, this is the desired behavior
                    if (!argsDictionary.ContainsKey(argName))
                    {
                        argsDictionary.Add(argName, argValue);
                    }
                    i++; // skip next string since it was added as an argument value
                }
            }

            return argsDictionary;
        }

        /// <summary>
        /// Get the argument from the dictionary <c>jobArgsDictionary</c> corresponding to <c>argName</c>.
        /// If not found, tries to get the value of environment variable for <c>envVariableKey</c>, if provided.
        /// If not found, throws ArgumentNullException
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string GetArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            string argValue;
            if (!jobArgsDictionary.TryGetValue(argName, out argValue) && !string.IsNullOrEmpty(fallbackEnvVariable))
            {
                argValue = Environment.GetEnvironmentVariable(fallbackEnvVariable);
            }

            if (string.IsNullOrEmpty(argValue))
            {
                if (string.IsNullOrEmpty(fallbackEnvVariable))
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.InvariantCulture, "Argument '{0}' was not passed", argName));
                }
                else
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.InvariantCulture, "Argument '{0}' was not passed. And, environment variable '{1}' was not set", argName, fallbackEnvVariable));
                }
            }

            return argValue;
        }

        /// <summary>
        /// Just calls GetArgsOrEnvVariable, but does not throw, instead returns null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string TryGetArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            try
            {
                return GetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Just calls TryGetArgument, but returns an int, if parsable, otherwise, null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static int? TryGetIntArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            int intArgument;
            string argumentString = TryGetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            if (!string.IsNullOrEmpty(argumentString) && int.TryParse(argumentString, out intArgument))
            {
                return intArgument;
            }
            return null;
        }

        /// <summary>
        /// Just calls TryGetArgument, but returns an bool, if parsable, otherwise, false
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a bool</returns>
        public static bool TryGetBoolArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            bool switchValue;
            string argumentString = TryGetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            if (!string.IsNullOrEmpty(argumentString) && bool.TryParse(argumentString, out switchValue))
            {
                return switchValue;
            }
            return false;
        }

        /// <summary>
        /// Just calls TryGetArgument, but returns a DateTime?, if parsable, otherwise, null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a DateTime?</returns>
        public static DateTime? TryGetDateTimeArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            DateTime switchValue;
            string argumentString = TryGetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            if (!string.IsNullOrEmpty(argumentString) && DateTime.TryParse(argumentString, out switchValue))
            {
                return switchValue;
            }
            return null;
        }
    }
}
