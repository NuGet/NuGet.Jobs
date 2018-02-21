// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Validation.PackageCompatibility.Core.Messages;

namespace Validation.PackageAuthoring.ValidatePackageReferenceCompatibility
{
    class PackageCompatibilityValidationMessageHandler : IMessageHandler<PackageCompatibilityValidationMessage>
    {
        private readonly HttpClient _httpClient;
        private readonly IValidatorStateService _validatorStateService;
        private readonly IPackageCompatibilityPackageReferenceValidator _packageCompatibilityPackageReferenceValidator;
        private readonly ILogger<PackageCompatibilityValidationMessageHandler> _logger;

        public Task<bool> HandleAsync(PackageCompatibilityValidationMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
