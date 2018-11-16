﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageMessageService : IMessageService<Package>
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<PackageMessageService> _logger;
        private readonly MessageServiceConfiguration _serviceConfiguration;

        public PackageMessageService(
            IMessageService messageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<PackageMessageService> logger)
        {
            _serviceConfiguration = new MessageServiceConfiguration(emailConfigurationAccessor);
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPublishedMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageAddedMessage = new PackageAddedMessage(
                                    _serviceConfiguration,
                                    package,
                                    galleryPackageUrl,
                                    packageSupportUrl,
                                    _serviceConfiguration.EmailConfiguration.EmailSettingsUrl,
                                    Array.Empty<string>());
        
            await _messageService.SendMessageAsync(packageAddedMessage);
        }

        public async Task SendValidationFailedMessageAsync(Package package, PackageValidationSet validationSet)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));
            validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);

            var packageValidationFailedMessage = new PackageValidationFailedMessage(
                                   _serviceConfiguration,
                                   package,
                                   validationSet,
                                   galleryPackageUrl,
                                   packageSupportUrl,
                                   _serviceConfiguration.EmailConfiguration.AnnouncementsUrl,
                                   _serviceConfiguration.EmailConfiguration.TwitterUrl);

            await _messageService.SendMessageAsync(packageValidationFailedMessage);
        }

        public async Task SendValidationTakingTooLongMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var packageValidationTakingTooLongMessage = new PackageValidationTakingTooLongMessage(
                                   _serviceConfiguration,
                                   package,
                                   _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion));

            await _messageService.SendMessageAsync(packageValidationTakingTooLongMessage);
        }
    }
}
