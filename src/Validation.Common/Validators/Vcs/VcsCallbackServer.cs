// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using NuGet.ApplicationInsights.Owin;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.Logging;
using NuGet.Services.VirusScanning.Vcs.Callback;
using Owin;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

[assembly: OwinStartup(typeof(VcsCallbackServer))]

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    public class VcsCallbackServer
    {
        internal static class VirusScanEventNames
        {
            /// <summary>
            /// The virus scan returned in an unknown state.
            /// The status of the virus scan should be investigated.
            /// </summary>
            internal const string Investigate = "Investigate";
            /// <summary>
            /// The validation ID specified by the request was not found.
            /// </summary>
            internal const string RequestNotFound = "RequestNotFound";
            /// <summary>
            /// The scan failed and did not complete.
            /// </summary>
            internal const string ScanFailed = "ScanFailed";
            /// <summary>
            /// The scan completed and has determined the package is clean.
            /// </summary>
            internal const string ScanPassed = "ScanPassed";
            /// <summary>
            /// The scan completed and determined the package is unclean and contains a virus or other issue.
            /// </summary>
            internal const string ScanUnclean = "ScanUnclean";
        }

        private readonly VcsStatusCallbackParser _callbackParser = new VcsStatusCallbackParser();

        private readonly ILogger _logger;

        private readonly PackageValidationTable _packageValidationTable;
        private readonly PackageValidationAuditor _packageValidationAuditor;
        private readonly INotificationService _notificationService;

        public VcsCallbackServer()
        {
            // Configure to get values from keyvault
            var configurationService = new ConfigurationService(new SecretReaderFactory());

            // Get configuration
            var cloudStorageAccount = CloudStorageAccount.Parse(configurationService.Get("DataStorageAccount").Result);
            var containerName = configurationService.Get("ContainerName").Result;

            // Services
            _packageValidationTable = new PackageValidationTable(cloudStorageAccount, containerName);
            _packageValidationAuditor = new PackageValidationAuditor(cloudStorageAccount, containerName);
            _notificationService = new NotificationService(cloudStorageAccount, containerName);

            // Set up AppInsights
            var instrumentationKey = configurationService.Get("AppInsightsInstrumentationKey").Result;
            Services.Logging.ApplicationInsights.Initialize(instrumentationKey);

            // Set up Logging
            _logger = LoggingSetup.CreateLoggerFactory().CreateLogger<VcsCallbackServer>();
        }

        public void Configuration(IAppBuilder app)
        {
            if (Services.Logging.ApplicationInsights.Initialized)
            {
                app.Use<RequestTrackingMiddleware>();
            }

            app.Run(Invoke);
        }

        public async Task Invoke(IOwinContext context)
        {
            if (context.Request.Method == "POST" && context.Request.ContentType.Contains("text/xml"))
            {
                // VCS callback request
                using (var bodyStreamReader = new StreamReader(context.Request.Body))
                {
                    var body = await bodyStreamReader.ReadToEndAsync();
                    var result = _callbackParser.ParseSoapMessage(body);

                    // Find our validation
                    Guid validationId;
                    PackageValidationEntity validationEntity = null;
                    if (Guid.TryParse(result.SrcId, out validationId))
                    {
                        validationEntity = await _packageValidationTable.GetValidationAsync(validationId);
                        if (validationEntity == null)
                        {
                            TrackValidationEvent(LogLevel.Warning, VirusScanEventNames.RequestNotFound, result, body);

                            // Notify us about the fact that no validation was found
                            await _notificationService.SendNotificationAsync(
                                "vcscallback-notfound",
                                "Validation " + validationId + " was not found.",
                                body);
                        }
                    }

                    // Determine state of the VCS callback
                    if (validationEntity != null)
                    {
                        // "The Request is in Manual State and the Request is cancelled."
                        // This denotes a manual verification is being carried out or has been carried out.
                        if (result.State == "Complete"
                            && (result.Result == "Canceled" || result.Result == "Cancelled"))
                        {
                            var services = result.Services?.Service;
                            if (services != null && services.Any(s => s.Name == "Scan" && s.State == "Complete" && s.Result == "Canceled"))
                            {
                                // Package scanned unclean
                                validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Failed);
                                await _packageValidationTable.StoreAsync(validationEntity);

                                var auditEntries = new List<PackageValidationAuditEntry>();
                                auditEntries.Add(new PackageValidationAuditEntry
                                {
                                    Timestamp = DateTimeOffset.UtcNow,
                                    ValidatorName = VcsValidator.ValidatorName,
                                    Message = "Package did not scan clean."
                                });

                                if (result.ResultReasons?.ResultReason != null)
                                {
                                    foreach (var resultReason in result.ResultReasons.ResultReason)
                                    {
                                        auditEntries.Add(new PackageValidationAuditEntry
                                        {
                                            Timestamp = DateTimeOffset.UtcNow,
                                            ValidatorName = VcsValidator.ValidatorName,
                                            Message = resultReason.RefId + " " + resultReason.Result + " " + resultReason.Determination
                                        });
                                    }
                                }

                                TrackValidationEvent(LogLevel.Error, VirusScanEventNames.ScanUnclean, result, validationEntity, body);

                                await _packageValidationAuditor.WriteAuditEntriesAsync(
                                    validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion, auditEntries);

                                // Notify
                                await _notificationService.SendNotificationAsync(
                                    $"vcscallback-notclean/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                    $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) returned {result.State} {result.Result}.",
                                    body);
                            }
                            else
                            {
                                TrackValidationEvent(LogLevel.Warning, VirusScanEventNames.Investigate, result, validationEntity, body);

                                // To investigate
                                await _notificationService.SendNotificationAsync(
                                    $"vcscallback-investigate/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                    $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) returned {result.State} {result.Result}.",
                                    body);
                            }
                        }

                        // "The Request is completed, with either of these four states: Results, Pass, PassWithInfo, PassManual"
                        // This denotes scan has completed and we have a pass (or results)
                        if (result.State == "Complete")
                        {
                            if (result.Result == "Pass" || result.Result == "PassWithInfo" || result.Result == "PassManual")
                            {
                                // The result is clean.
                                validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Succeeded);
                                await _packageValidationTable.StoreAsync(validationEntity);

                                TrackValidationEvent(LogLevel.Information, VirusScanEventNames.ScanPassed, result, validationEntity, body);

                                await _packageValidationAuditor.WriteAuditEntryAsync(validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion,
                                    new PackageValidationAuditEntry
                                    {
                                        Timestamp = DateTimeOffset.UtcNow,
                                        ValidatorName = VcsValidator.ValidatorName,
                                        Message = "Package scanned clean."
                                    });
                            }
                            else if (result.Result == "Results" || result.Result == "Fail")
                            {
                                // Potential issue, report back
                                validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Failed);
                                await _packageValidationTable.StoreAsync(validationEntity);

                                var auditEntries = new List<PackageValidationAuditEntry>();
                                auditEntries.Add(new PackageValidationAuditEntry
                                {
                                    Timestamp = DateTimeOffset.UtcNow,
                                    ValidatorName = VcsValidator.ValidatorName,
                                    Message = "Package scan failed."
                                });

                                if (result.ResultReasons?.ResultReason != null)
                                {
                                    foreach (var resultReason in result.ResultReasons.ResultReason)
                                    {
                                        auditEntries.Add(new PackageValidationAuditEntry
                                        {
                                            Timestamp = DateTimeOffset.UtcNow,
                                            ValidatorName = VcsValidator.ValidatorName,
                                            Message = resultReason.RefId + " " + resultReason.Result + " " + resultReason.Determination
                                        });
                                    }
                                }

                                TrackValidationEvent(LogLevel.Error, VirusScanEventNames.ScanFailed, result, validationEntity, body);

                                await _packageValidationAuditor.WriteAuditEntriesAsync(
                                    validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion, auditEntries);

                                // Notify
                                await _notificationService.SendNotificationAsync(
                                    $"vcscallback-failed/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                    $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) did not scan clean.",
                                    body);
                            }
                        }
                    }
                }
                // The VCS caller requires a SOAP response.
                context.Response.ContentType = "text/xml";
                await context.Response.WriteAsync(@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <StatusChangedResponse xmlns=""http://roq/"" />
  </soap:Body>
</soap:Envelope>");
            }
            else if (context.Request.Method == "GET")
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("OK");
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }
        
        private void TrackValidationEvent(LogLevel level, string eventName, Job job, string requestBody)
        {
            var validationEntityLog = "{ValidationId} missing";

            var validationEntityParameters = new object[]
            {
                job.SrcId
            };

            TrackValidationEventCore(level, eventName, job, validationEntityLog, validationEntityParameters, requestBody);
        }

        private void TrackValidationEvent(LogLevel level, string eventName, Job job, PackageValidationEntity entity, string requestBody)
        {
            var validationEntityLog =
                "{ValidationId} for {PackageId} version {PackageVersion} created on {PackageValidationCreated}";

            var validationEntityParameters = new object[]
            {
                entity.ValidationId, entity.PackageId, entity.PackageVersion, entity.Created,
            };

            TrackValidationEventCore(level, eventName, job, validationEntityLog, validationEntityParameters, requestBody);
        }

        private void TrackValidationEventCore(LogLevel level, string eventName, Job job, string validationEntityLog,
            object[] validationEntityParameters, string requestBody)
        {
            var logMessage = string.Join(" - ",
                validationEntityLog,
                "{EventName}",
                "Job {JobState} {JobResult} ran from {JobStartDate} to {JobEndDate}",
                "Scan {RqsRequestId} {RqsJobId} ended at {ScanEndDate}",
                "{CustomerLogsLocation}", "{LogFilesPath}", "{SourceBitsLocation}",
                "{RequestBody}");

            var parameters = new object[]
            {
                eventName,
                job.State, job.Result, job.JobStartDate, job.JobEndDate,
                job.RqsRequestId, job.RqsJobId, job.ScanEndDate,
                job.CustomerLogsLocation, job.LogFilesPath, job.SourceBitsLocation,
                requestBody
            };

            parameters = validationEntityParameters.Concat(parameters).ToArray();

            switch (level)
            {
                case LogLevel.Trace:
                    _logger.LogTrace(logMessage, parameters);
                    break;
                case LogLevel.Debug:
                    _logger.LogDebug(logMessage, parameters);
                    break;
                case LogLevel.Information:
                    _logger.LogInformation(logMessage, parameters);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(logMessage, parameters);
                    break;
                case LogLevel.Error:
                    _logger.LogError(logMessage, parameters);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(logMessage, parameters);
                    break;
            }
        }
    }
}