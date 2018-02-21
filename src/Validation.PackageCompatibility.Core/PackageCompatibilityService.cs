// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Services.Validation;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;

namespace Validation.PackageCompatibility.Core.Storage
{
    public class PackageCompatibilityService : IPackageCompatibilityService
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ILogger<PackageCompatibilityService> _logger;

        public PackageCompatibilityService(
            IValidationEntitiesContext validationContext,
            ILogger<PackageCompatibilityService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetPackageCompatibilityState(
           Guid validationId,
           IEnumerable<PackLogMessage> messages)
        {
            foreach (var log in messages)
            {
                _validationContext.PackageCompatibilityIssues.Add(
                              new PackageCompatibilityIssue()
                              {
                                  ClientIssueCode = log.Code.ToString(),
                                  Message = log.Message,
                                  PackageValidationKey = validationId
                              }
                          );
            }
            try
            {
                await _validationContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning("trouble saving changes async - {0}", e.Message);
            }
        }
    }
}
