// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.VirusScanning.Vcs.Callback;
using Owin;

[assembly: OwinStartup(typeof(VcsCallbackServerStartup))]

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    public class VcsCallbackServerStartup
    {
        private readonly VcsStatusCallbackParser _callbackParser = new VcsStatusCallbackParser();

        private readonly PackageValidationTable _packageValidationTable;
        private readonly PackageValidationAuditor _packageValidationAuditor;
        private readonly INotificationService _notificationService;
        private readonly string _instrumentationKey;

        public VcsCallbackServerStartup()
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

            // Setup telemetry
            _instrumentationKey = configurationService.Get("AppInsightsInstrumentationKey").Result;
        }

        public void Configuration(IAppBuilder app)
        {
            app.Use<GlobalExceptionHandlerMiddleware>();

            if (string.IsNullOrEmpty(_instrumentationKey))
            {
                TelemetryConfiguration.Active.DisableTelemetry = true;
            }
            else
            {
                TelemetryConfiguration.Active.InstrumentationKey = _instrumentationKey;

                app.Use<ApplicationInsightsMiddleware>();
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
                            var telemetry = new EventTelemetry(TelemetryConstants.RequestNotFound);
                            telemetry.Properties.Add(TelemetryConstants.RequestId, result.SrcId);
                            telemetry.Properties.Add(TelemetryConstants.RequestBody, body);

                            TelemetryClient.TrackEvent(telemetry);

                            // Notify us about the fact that no valiation was found
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

                                TrackEvent(TelemetryConstants.ScanUnclean, result, validationEntity, body);

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
                                TrackEvent(TelemetryConstants.Investigate, result, validationEntity, body);

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

                                TrackEvent(TelemetryConstants.ScanPassed, result, validationEntity, body);

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

                                TrackEvent(TelemetryConstants.ScanFailed, result, validationEntity, body);

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

        private static void TrackEvent(string eventName, Job job, PackageValidationEntity entity, string requestBody)
        {
            var telemetry = new EventTelemetry(eventName);

            telemetry.Properties.Add(TelemetryConstants.RequestId, job.SrcId);
            telemetry.Properties.Add(TelemetryConstants.State, job.State);
            telemetry.Properties.Add(TelemetryConstants.Result, job.Result);
            telemetry.Properties.Add(TelemetryConstants.PackageId, entity.PackageId);
            telemetry.Properties.Add(TelemetryConstants.PackageVersion, entity.PackageVersion);
            telemetry.Properties.Add(TelemetryConstants.RqsJobId, job.RqsJobId);
            telemetry.Properties.Add(TelemetryConstants.RqsRequestId, job.RqsRequestId);
            telemetry.Properties.Add(TelemetryConstants.JobStartDate, job.JobStartDate);
            telemetry.Properties.Add(TelemetryConstants.ScanEndDate, job.ScanEndDate);
            telemetry.Properties.Add(TelemetryConstants.JobEndDate, job.JobEndDate);
            telemetry.Properties.Add(TelemetryConstants.CustomerLogsLocation, job.CustomerLogsLocation);
            telemetry.Properties.Add(TelemetryConstants.LogFilesPath, job.LogFilesPath);
            telemetry.Properties.Add(TelemetryConstants.SourceBitsLocation, job.SourceBitsLocation);
            telemetry.Properties.Add(TelemetryConstants.RequestBody, requestBody);

            TelemetryClient.TrackEvent(telemetry);
        }
    }
}