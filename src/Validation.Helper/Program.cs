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

            using (logger.BeginScope("Processing action {Action} scope id: {GUID}", action, Guid.NewGuid()))
            {
                switch (action)
                {
                case Action.Rescan:
                    Rescan(arguments, azureAccount, containerName);
                    break;

                case Action.MarkSuccess:
                    MarkSuccessful(arguments, azureAccount, containerName);
                    break;
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
                    $"-{JobArgumentNames.Action} ({Action.Rescan.ToString()}|{Action.MarkSuccess.ToString()}) " +
                    $"[-{JobArgumentNames.StoreName} (My|Root|TrustedPeople|TrustedPublisher|AddressBook|AuthRoot|CertificateAuthority|Disallowed)] " +
                    $"[-{JobArgumentNames.StoreLocation} (LocalMachine|CurrentUser)] " +
                    $"[-{JobArgumentNames.ValidateCertificate} (true|false)] " +
                    $"[-{JobArgumentNames.InstrumentationKey} <AI instrumentation key>]",
                Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));

            Console.WriteLine();
            Console.WriteLine($"'-{JobArgumentNames.Action} {Action.Rescan.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{JobArgumentNames.PackageId} <package id> - case sensitive");
            Console.WriteLine($"\t-{JobArgumentNames.PackageVersion} <package version> - case sensitive");
            Console.WriteLine();
            Console.WriteLine($"'-{JobArgumentNames.Action} {Action.MarkSuccess.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{JobArgumentNames.PackageId} <package id> - case sensitive");
            Console.WriteLine($"\t-{JobArgumentNames.PackageVersion} <package version> - case sensitive");
            Console.WriteLine($"\t-{JobArgumentNames.ValidationId} <validation Id (GUID)>");
            Console.WriteLine($"\t-{JobArgumentNames.Comment} <comment> - please include your alias");
        }

        private static void Rescan(IDictionary<string, string> arguments, CloudStorageAccount azureAcc, string container)
        {
            string packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
            string packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);

            logger.LogInformation($"Creating rescan request for {{{TraceConstant.PackageId}}} {{{TraceConstant.PackageVersion}}}", packageId, packageVersion);

            using (var httpClient = new HttpClient())
            {
                var feed = new NuGetV2Feed(httpClient, loggerFactory);

                var url = GetPackageUrl(packageId, packageVersion);
                NuGetPackage package = feed.GetPackagesAsync(url).Result.FirstOrDefault();

                if (package == null)
                {
                    logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} {{{TraceConstant.PackageVersion}}}. Terminating.");
                    return;
                }

                logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} {{{TraceConstant.PackageVersion}}}", package.Id, package.Version);

                var packageValidationService = new PackageValidationService(azureAcc, container, loggerFactory);
                logger.LogInformation($"Submitting rescan request for {{{TraceConstant.PackageId}}} {{{TraceConstant.PackageVersion}}}", package.Id, package.Version);
                packageValidationService.StartValidationProcessAsync(package, new[] { VcsValidator.ValidatorName }).Wait();
                logger.LogInformation($"Done submitting rescan request for {{{TraceConstant.PackageId}}} {{{TraceConstant.PackageVersion}}}", package.Id, package.Version);
            }
        }

        private static void MarkSuccessful(IDictionary<string, string> arguments, CloudStorageAccount azureAcc, string container)
        {
            string packageId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageId);
            string packageVersion = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.PackageVersion);
            string validationId = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.ValidationId);
            string comment = JobConfigurationManager.GetArgument(arguments, JobArgumentNames.Comment);
        }

        private static Uri GetPackageUrl(string packageId, string packageVersion)
        {
            return new Uri($"https://www.nuget.org/api/v2/Packages?$filter=Id%20eq%20'{packageId}'%20and%20Version%20eq%20'{packageVersion}'%20and%20true");
        }
    }
}
