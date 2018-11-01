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
    public class SymbolsPackageMessageService : IMessageService<SymbolPackage>
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<SymbolsPackageMessageService> _logger;
        private readonly MessageServiceConfiguration _serviceConfiguration;

        public SymbolsPackageMessageService(
            IMessageService messageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<SymbolsPackageMessageService> logger)
        {
            _serviceConfiguration = new MessageServiceConfiguration(emailConfigurationAccessor);
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPublishedMessageAsync(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);
            var symbolPackageAddedMessage = new SymbolPackageAddedMessage(
                                    _serviceConfiguration,
                                    symbolPackage,
                                    galleryPackageUrl,
                                    packageSupportUrl,
                                    _serviceConfiguration.EmailConfiguration.EmailSettingsUrl,
                                    Array.Empty<string>());
            await _messageService.SendMessageAsync(symbolPackageAddedMessage);
        }

        public async Task  SendValidationFailedMessageAsync(SymbolPackage symbolPackage, PackageValidationSet validationSet)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }
            validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);

            var symbolPackageValidationFailedMessage = new SymbolPackageValidationFailedMessage(
                                   _serviceConfiguration,
                                   symbolPackage,
                                   validationSet,
                                   galleryPackageUrl,
                                   packageSupportUrl,
                                   _serviceConfiguration.EmailConfiguration.AnnouncementsUrl,
                                   _serviceConfiguration.EmailConfiguration.TwitterUrl);

            await _messageService.SendMessageAsync(symbolPackageValidationFailedMessage);
        }

        public async Task SendValidationTakingTooLongMessageAsync(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }
            var symbolPackageValidationTakingTooLongMessage = new SymbolPackageValidationTakingTooLongMessage(
                                   _serviceConfiguration,
                                   symbolPackage,
                                   _serviceConfiguration.GalleryPackageUrl(symbolPackage.Package.PackageRegistration.Id, symbolPackage.Package.NormalizedVersion));

            await _messageService.SendMessageAsync(symbolPackageValidationTakingTooLongMessage);
        }
    }
}
