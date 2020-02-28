// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;
using Stats.AzureCdnLogs.Common.Collect;
using Stats.CollectAzureChinaCDNLogs;

namespace TestCdnLogFile
{
    class Program
    {
        static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task<int> MainAsync(string[] args)
        {
            using (var loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true)))
            {
                var programLogger = loggerFactory.CreateLogger<Program>();

                if (args.Length < 2)
                {
                    programLogger.LogError($"Usage: {AppDomain.CurrentDomain.FriendlyName} <China | Global> <PATH OR PATTERN>");
                    return 1;
                }

                var mode = args[0];
                var pathOrPattern = args[1];

                programLogger.LogInformation("Mode: {Mode}", mode);

                var directoryToTest = Path.GetDirectoryName(pathOrPattern);
                var patternToTest = Path.GetFileName(pathOrPattern);

                IEnumerable<string> filesToTest;
                if (patternToTest.Contains("*"))
                {
                    programLogger.LogInformation("Pattern: {File}", pathOrPattern);
                    filesToTest = Directory.EnumerateFiles(directoryToTest, patternToTest, SearchOption.TopDirectoryOnly).ToList();
                    programLogger.LogInformation("File count: {Count}", filesToTest.Count());
                }
                else
                {
                    filesToTest = new[] { pathOrPattern };
                }

                foreach (var currentFile in filesToTest)
                {
                    programLogger.LogInformation("File: {File}", currentFile);

                    try
                    {
                        switch (mode.ToLowerInvariant())
                        {
                            case "global":
                                TestGlobal(currentFile, programLogger);
                                break;
                            case "china":
                                await TestChinaAsync(currentFile, loggerFactory, programLogger);
                                break;
                            default:
                                programLogger.LogError("Unknown mode: {Mode}", mode);
                                return 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        programLogger.LogError(ex, "An error occurred while running the program.");
                    }
                }

                return 0;
            }
        }

        private static void TestGlobal(string fileToTest, ILogger<Program> programLogger)
        {
            using (var fileStream = File.OpenRead(fileToTest))
            {
                Stream streamToRead = fileToTest.Contains(".gz") ? (Stream)new GZipInputStream(fileStream) : fileStream;
                try
                {
                    Stats.CollectAzureCdnLogs.Job.ProcessLogStream(
                           streamToRead,
                           Stream.Null,
                           Path.GetFileName(fileToTest),
                           programLogger);
                }
                finally
                {
                    streamToRead?.Dispose();
                }
            }
        }

        private static async Task TestChinaAsync(string fileToTest, ILoggerFactory loggerFactory, ILogger<Program> programLogger)
        {
            var logSource = new TestLogSource(fileToTest);
            var logDestination = new TestLogDestination();

            var chinaCollector = new ChinaStatsCollector(
                logSource,
                logDestination,
                loggerFactory.CreateLogger<ChinaStatsCollector>());

            var exception = await chinaCollector.TryProcessAsync(1, s => s, ContentType.GZip, ContentType.GZip, CancellationToken.None);
            if (exception != null)
            {
                programLogger.LogError(exception, "An error occurred while processing the log file.");
            }
        }
    }
}
