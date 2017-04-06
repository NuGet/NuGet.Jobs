// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Common.Validators.Unzip
{
    public class UnzipValidator
        : ValidatorBase, IValidator
    {
        public const string ValidatorName = "validator-unzip";

        private readonly ILogger<UnzipValidator> _logger = Services.Logging.LoggingSetup.CreateLoggerFactory().CreateLogger<UnzipValidator>();

        public override string Name
        {
            get
            {
                return ValidatorName;
            }
        }
        
        public override async Task<ValidationResult> ValidateAsync(PackageValidationMessage message, List<PackageValidationAuditEntry> auditEntries)
        {
            var temporaryFile = Path.GetTempFileName();

            using (var httpClient = new HttpClient())
            {
                try
                {
                    using (var packageStream = await httpClient.GetStreamAsync(message.Package.DownloadUrl))
                    {
                        using (var packageFileStream = File.Open(temporaryFile, FileMode.OpenOrCreate))
                        {
                            await packageStream.CopyToAsync(packageFileStream);

                            WriteAuditEntry(auditEntries, $"Downloaded package from {message.Package.DownloadUrl}");

                            packageFileStream.Position = 0;
                            
                            using (var packageZipStream = Package.Open(packageFileStream))
                            {
                                var parts = packageZipStream.GetParts();
                                WriteAuditEntry(auditEntries, $"Found {parts.Count()} parts in package.");

                                return ValidationResult.Succeeded;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.TrackValidatorException(ValidatorName, ex, message.PackageId, message.PackageVersion, message.ValidationId);
                    WriteAuditEntry(auditEntries, $"Exception thrown during validation - {ex.Message}\r\n{ex.StackTrace}");
                    return ValidationResult.Failed;
                }
                finally
                {
                    try
                    {
                        File.Delete(temporaryFile);
                    }
                    catch
                    {
                        // best-effort
                    }
                }
            }
        }
    }
}