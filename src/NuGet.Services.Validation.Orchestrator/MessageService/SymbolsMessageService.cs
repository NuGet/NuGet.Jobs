// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    //ToDo: https://github.com/NuGet/NuGetGallery/issues/6255
    public class SymbolsPackageMessageService : MessageServiceConfiguration, IMessageService<SymbolPackage>
    {
        private readonly ICoreMessageService _coreMessageService;
        private readonly ILogger<SymbolsPackageMessageService> _logger;

        public SymbolsPackageMessageService(
            ICoreMessageService coreMessageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<SymbolsPackageMessageService> logger) : base (emailConfigurationAccessor)
        {
            _coreMessageService = coreMessageService ?? throw new ArgumentNullException(nameof(coreMessageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPublishedMessageAsync(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            var galleryPackageUrl = GalleryPackageUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);
            var packageSupportUrl = PackageSupportUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);

            await _coreMessageService.SendSymbolPackageAddedNoticeAsync(symbolPackage, galleryPackageUrl, packageSupportUrl, _emailConfiguration.EmailSettingsUrl);
        }

        public async Task  SendValidationFailedMessageAsync(SymbolPackage symbolPackage, PackageValidationSet validationSet)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }
            validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));

            var galleryPackageUrl = GalleryPackageUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);
            var packageSupportUrl = PackageSupportUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion);

            await _coreMessageService.SendSymbolPackageValidationFailedNoticeAsync(symbolPackage, validationSet, galleryPackageUrl, packageSupportUrl, _emailConfiguration.AnnouncementsUrl, _emailConfiguration.TwitterUrl);
        }

        public async Task SendValidationTakingTooLongMessageAsync(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            await _coreMessageService.SendValidationTakingTooLongNoticeAsync(symbolPackage, GalleryPackageUrl(symbolPackage.Id, symbolPackage.Package.NormalizedVersion));
        }
    }
}
