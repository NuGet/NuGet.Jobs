// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation.Helper
{
    class Program
    {
        private static ILoggerFactory loggerFactory;
        private static ILogger<Program> logger;

        private static void Main(string[] args)
        {
            if (args.Count() < 16)  // 6 common required argument pairs + at least 2 action-specific
            {
                PrintUsage();
                return;
            }

            ISecretReaderFactory secretReaderFactory = new SecretReaderFactory();
            IDictionary<string, string> arguments = JobConfigurationManager.GetJobArgsDictionary(args, "Validation.Helper", secretReaderFactory);

            if (!ApplicationInsights.Initialized)
            {
                string instrumentationKey = JobConfigurationManager.TryGetArgument(arguments, JobArgumentNames.InstrumentationKey);
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    ApplicationInsights.Initialize(instrumentationKey);
                }
            }

            loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
            logger = loggerFactory.CreateLogger<Program>();

            string azureStorageConnectionString = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.DataStorageAccount);
            string containerName = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.ContainerName);

            CloudStorageAccount azureAccount = CloudStorageAccount.Parse(azureStorageConnectionString);

            Action action = ParseEnum<Action>(JobConfigurationManager.GetArgument(arguments, JobArgumentNames.Action));

            using (logger.BeginScope("Processing action {Action} scope id: {RunTraceId}", action, Guid.NewGuid()))
            {
                try
                {
                    switch (action)
                    {
                    case Action.Rescan:
                        Rescan(arguments, azureAccount, containerName);
                        break;

                    case Action.MarkClean:
                        MarkClean(arguments, azureAccount, containerName);
                        break;
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(TraceEvent.HelperFailed, e, "Failed to run action");
                }
            }
        }

        private static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: {0} " +
                    $"-{JobArgumentNames.VaultName} <KeyVault name> " +
                    $"-{JobArgumentNames.ClientId} <KeyVault clientId> " +
                    $"-{JobArgumentNames.CertificateThumbprint} <KeyVault certificate thumbprint> " +
                    $"-{JobArgumentNames.DataStorageAccount} <Azure Blob Storage connection string> " +
                    $"-{JobArgumentNames.ContainerName} <validation job container name> " +
                    $"-{JobArgumentNames.Action} ({Action.Rescan.ToString()}|{Action.MarkClean.ToString()}) " +
                    $"[-{JobArgumentNames.StoreName} (My|Root|TrustedPeople|TrustedPublisher|AddressBook|AuthRoot|CertificateAuthority|Disallowed)] " +
                    $"[-{JobArgumentNames.StoreLocation} (LocalMachine|CurrentUser)] " +
                    $"[-{JobArgumentNames.ValidateCertificate} (true|false)] " +
                    $"[-{JobArgumentNames.InstrumentationKey} <AI instrumentation key>]",
                Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));

            Console.WriteLine();
            Console.WriteLine($"'-{JobArgumentNames.Action} {Action.Rescan.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{JobArgumentNames.PackageId} <package id>");
            Console.WriteLine($"\t-{JobArgumentNames.PackageVersion} <package version>");
            Console.WriteLine();
            Console.WriteLine($"'-{JobArgumentNames.Action} {Action.MarkClean.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{JobArgumentNames.PackageId} <package id>");
            Console.WriteLine($"\t-{JobArgumentNames.PackageVersion} <package version>");
            Console.WriteLine($"\t-{JobArgumentNames.ValidationId} <validation Id (GUID)>");
            Console.WriteLine($"\t-{JobArgumentNames.Comment} <comment> - please include your alias");
        }

        private static void Rescan(IDictionary<string, string> arguments, CloudStorageAccount azureAccount, string container)
        {
            string packageId;
            string packageVersion;
            try
            {
                packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
                packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);
            }
            catch (Exception e)
            {
                logger.LogError(TraceEvent.FailedToProcessArguments, 
                    e, 
                    "Exception while processing {Action} arguments", 
                    Action.Rescan);
                return;
            }

            logger.LogInformation($"Creating rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                packageId,
                packageVersion);

            NuGetPackage package = GetPackage(packageId, packageVersion);
            if (package == null)
            {
                logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    packageId, 
                    packageVersion);
                return;
            }
            logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            var packageValidationService = new PackageValidationService(azureAccount, container, loggerFactory);
            logger.LogInformation($"Submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);
            packageValidationService.StartValidationProcessAsync(package, new[] { VcsValidator.ValidatorName }).Wait();
            logger.LogInformation($"Done submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);
        }

        private static void MarkClean(IDictionary<string, string> arguments, CloudStorageAccount azureAccount, string container)
        {
            string packageId;
            string packageVersion;
            string validationIdStr;
            string comment;
            Guid validationId;
            try
            {
                packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
                packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);
                validationIdStr = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.ValidationId);
                validationId = Guid.Parse(validationIdStr);
                comment = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.Comment);
            }
            catch (Exception e)
            {
                logger.LogError(TraceEvent.FailedToProcessArguments, 
                    e, 
                    "Exception while processing {Action} arguments", 
                    Action.MarkClean);
                return;
            }

            logger.LogInformation($"Starting creating successful scan entry for the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                packageId,
                packageVersion);

            NuGetPackage package = GetPackage(packageId, packageVersion);
            if (package == null)
            {
                logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    packageId,
                    packageVersion);
                return;
            }
            logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            /// <see cref="PackageValidationService.StartValidationProcessAsync(NuGetPackage, string[])"/>
            packageVersion = package.NormalizedVersion ?? package.Version;

            var packageValidationAuditor = new PackageValidationAuditor(azureAccount, container, loggerFactory);
            PackageValidationAuditEntry[] entries = new[] {new PackageValidationAuditEntry {
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorName = VcsValidator.ValidatorName,
                Message = $"Manually marking the package as scanned clean, comment: {comment}",
                EventId = ValidationEvent.PackageClean,
            }};

            logger.LogInformation($"Marking the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}} " +
                    $"as clean with comment: {{{TraceConstant.Comment}}}",
                package.Id,
                packageVersion,
                comment);
            packageValidationAuditor.WriteAuditEntriesAsync(validationId, package.Id, packageVersion, entries).Wait();
        }

        private static NuGetPackage GetPackage(string packageId, string packageVersion)
        {
            using (var httpClient = new HttpClient())
            {
                var feed = new NuGetV2Feed(httpClient, loggerFactory);

                var url = GetPackageUrl(packageId, packageVersion);
                return feed.GetPackagesAsync(url).Result.FirstOrDefault();
            }
        }

        private static Uri GetPackageUrl(string packageId, string packageVersion)
        {
            return new Uri("https://www.nuget.org/api/v2/Packages?" +
                $"$filter=Id eq '{packageId}' and Version eq '{packageVersion}' and true");
        }
    }
}
