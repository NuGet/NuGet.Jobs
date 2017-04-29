// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation.Helper
{
    public class Job : JobBase
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<Job> _logger;
        private CloudStorageAccount _cloudStorageAccount;
        private string _containerName;
        private Action _action;
        private IDictionary<string, string> _jobArgsDictionary;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            _jobArgsDictionary = jobArgsDictionary;
            try
            {
                if (!ApplicationInsights.Initialized)
                {
                    string instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                    if (!string.IsNullOrWhiteSpace(instrumentationKey))
                    {
                        ApplicationInsights.Initialize(instrumentationKey);
                    }
                }

                // A hack to prevent Trace.<something> from printing to console. This code uses ILogger,
                // JobRunner unfortunately uses Trace and prints stuff that is not related to this tool. 
                // When (and if) JobRunner and all other jobs are updatedto use ILogger, this call should 
                // be removed.
                DisableTrace();

                _loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
                _logger = _loggerFactory.CreateLogger<Job>();

                string azureStorageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
                _containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.ContainerName);
                _cloudStorageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);

                _action = ParseEnum<Action>(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.Action));
                return true;
            }
            catch (Exception e)
            {
                if (_logger != null)
                {
                    _logger.LogError(TraceEvent.CommandLineProcessingFailed, e, "Exception occurred while processing command line arguments");
                }
                else
                {
                    Trace.TraceError("Exception occurred while processing command line arguments: {0}", e);
                }

                PrintUsage();
            }

            return false;
        }

        private static void DisableTrace()
        {
            int i = 0;
            while (i < Trace.Listeners.Count)
            {
                if (Trace.Listeners[i] is JobTraceListener)
                {
                    Trace.Listeners.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }
        }

        public async override Task<bool> Run()
        {
            using (_logger.BeginScope("Processing action {Action} scope id: {RunTraceId}", _action, Guid.NewGuid()))
            {
                try
                {
                    switch (_action)
                    {
                    case Action.Rescan:
                        await Rescan(_jobArgsDictionary, _cloudStorageAccount, _containerName);
                        break;

                    case Action.MarkClean:
                        await MarkClean(_jobArgsDictionary, _cloudStorageAccount, _containerName);
                        break;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    _logger.LogError(TraceEvent.HelperFailed, e, "Failed to run action");
                }
            }

            return false;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: {0} " +
                    $"-{JobArgumentNames.VaultName} <KeyVault name> " +
                    $"-{JobArgumentNames.ClientId} <KeyVault clientId> " +
                    $"-{JobArgumentNames.CertificateThumbprint} <KeyVault certificate thumbprint> " +
                    $"-{JobArgumentNames.LogsAzureStorageConnectionString} <azure logs blob storage connection string> " +
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
            Console.WriteLine($"\t-{JobArgumentNames.Alias} <alias>");
            Console.WriteLine($"\t-{JobArgumentNames.Comment} <comment>");
        }

        private static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }

        private async Task Rescan(IDictionary<string, string> arguments, CloudStorageAccount azureAccount, string container)
        {
            string packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
            string packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);

            _logger.LogInformation($"Creating rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                packageId,
                packageVersion);

            NuGetPackage package = await GetPackage(packageId, packageVersion);
            if (package == null)
            {
                _logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    packageId,
                    packageVersion);
                return;
            }
            _logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            var packageValidationService = new PackageValidationService(azureAccount, container, _loggerFactory);
            _logger.LogInformation($"Submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);
            await packageValidationService.StartValidationProcessAsync(package, new[] { VcsValidator.ValidatorName });
            _logger.LogInformation($"Done submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);
        }

        private async Task MarkClean(IDictionary<string, string> arguments, CloudStorageAccount azureAccount, string container)
        {
            string packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
            string packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);
            string validationIdStr = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.ValidationId);
            Guid validationId = Guid.Parse(validationIdStr);
            string comment = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.Comment);
            string alias = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.Alias);

            _logger.LogInformation($"Starting creating successful scan entry for the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                packageId,
                packageVersion);

            NuGetPackage package = await GetPackage(packageId, packageVersion);
            if (package == null)
            {
                _logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    packageId,
                    packageVersion);
                return;
            }
            _logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            packageVersion = package.GetVersion();

            var packageValidationAuditor = new PackageValidationAuditor(azureAccount, container, _loggerFactory);
            PackageValidationAuditEntry[] entries = new[] {new PackageValidationAuditEntry {
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorName = VcsValidator.ValidatorName,
                Message = $"{alias} marked the package as scanned clean, comment: {comment}",
                EventId = ValidationEvent.PackageClean,
            }};

            _logger.LogInformation($"Marking the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}} " +
                    $"as clean with comment: {{{TraceConstant.Comment}}}. " +
                    $"Requested by {{{TraceConstant.Alias}}}",
                package.Id,
                packageVersion,
                comment,
                alias);
            await packageValidationAuditor.WriteAuditEntriesAsync(validationId, package.Id, packageVersion, entries);
        }

        private async Task<NuGetPackage> GetPackage(string packageId, string packageVersion)
        {
            using (var httpClient = new HttpClient())
            {
                var feed = new NuGetV2Feed(httpClient, _loggerFactory.CreateLogger<NuGetV2Feed>());

                var url = GetPackageUrl(packageId, packageVersion);
                return (await feed.GetPackagesAsync(url)).FirstOrDefault();
            }
        }

        private static Uri GetPackageUrl(string packageId, string packageVersion)
        {
            return new Uri("https://www.nuget.org/api/v2/Packages?" +
                $"$filter=Id eq '{packageId}' and Version eq '{packageVersion}' and true");
        }
    }
}
