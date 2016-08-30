﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.KeyVault;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    /// <summary>
    /// This class is used to retrieve and expose the known Azure configuration settings
    /// from Environment Variables and command line arguments
    /// </summary>
    public static class JobConfigurationManager
    {
        /// <summary>
        /// Parses the string[] of <c>args</c> passed into the job into a dictionary of string, string.
        /// Expects the string[] to be set of pairs of argumentName and argumentValue, where, argumentName start with a hyphen
        /// </summary>
        /// <param name="commandLineArgs">Arguments passed to the job via commandline or environment variable settings</param>
        /// <param name="jobName">Jobname to be used to infer environment variable settings</param>
        /// <param name="secretReaderFactory">Creates a secret reader.</param>
        /// <returns>Returns a dictionary of arguments</returns>
        public static IArgumentsDictionary GetJobArgsDictionary(string[] commandLineArgs, string jobName, ISecretReaderFactory secretReaderFactory)
        {
            if (secretReaderFactory == null)
            {
                throw new ArgumentNullException(nameof(secretReaderFactory));
            }

            var argsDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allArgsList = commandLineArgs.ToList();
            if (allArgsList.Count == 0)
            {
                Trace.TraceInformation("No command-line arguments provided.");
            }
            else
            {
                Trace.TraceInformation("Total number of arguments : " + allArgsList.Count);

                // Arguments are expected to be a set of pairs, where each pair is of the form '-<argName> <argValue>'
                // Or, in singles as a switch '-<switch>'

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
            }

            return CreateArgumentsDictionary(secretReaderFactory, argsDictionary);
        }

        /// <summary>
        /// Get the argument from the dictionary <c>jobArgsDictionary</c> corresponding to <c>argName</c>.
        /// If not found, throws ArgumentNullException
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string GetArgument(IDictionary<string, string> jobArgsDictionary, string argName)
        {
            string argValue;

            if (!jobArgsDictionary.TryGetValue(argName, out argValue) || string.IsNullOrEmpty(argValue))
            {
                throw new ArgumentNullException($"Argument '{argName}' was not passed.");
            }

            return argValue;
        }

        /// <summary>
        /// Just calls GetArgsOrEnvVariable, but does not throw, instead returns null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string TryGetArgument(IDictionary<string, string> jobArgsDictionary, string argName)
        {
            try
            {
                return GetArgument(jobArgsDictionary, argName);
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
        /// <returns>Returns the argument value as a string</returns>
        public static int? TryGetIntArgument(IDictionary<string, string> jobArgsDictionary, string argName)
        {
            int intArgument;
            string argumentString = TryGetArgument(jobArgsDictionary, argName);
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
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the argument value as a bool</returns>
        public static bool TryGetBoolArgument(IDictionary<string, string> jobArgsDictionary, string argName, bool defaultValue = false)
        {
            bool switchValue;
            string argumentString = TryGetArgument(jobArgsDictionary, argName);
            if (!string.IsNullOrEmpty(argumentString) && bool.TryParse(argumentString, out switchValue))
            {
                return switchValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Just calls TryGetArgument, but returns a DateTime?, if parsable, otherwise, null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <returns>Returns the argument value as a DateTime?</returns>
        public static DateTime? TryGetDateTimeArgument(IDictionary<string, string> jobArgsDictionary, string argName)
        {
            DateTime switchValue;
            string argumentString = TryGetArgument(jobArgsDictionary, argName);
            if (!string.IsNullOrEmpty(argumentString) && DateTime.TryParse(argumentString, out switchValue))
            {
                return switchValue;
            }
            return null;
        }

        private static IArgumentsDictionary CreateArgumentsDictionary(ISecretReaderFactory secretReaderFactory, Dictionary<string, string> argsDictionary)
        {
            var secretReader = secretReaderFactory.CreateSecterReader(argsDictionary);
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReader);

            if (secretReader == null)
            {
                throw new ApplicationException("Could not create a secret reader. Please check your configuration.");
            }

            return new RefreshingArgumentsDictionary(secretInjector, argsDictionary);
        }
    }
}
