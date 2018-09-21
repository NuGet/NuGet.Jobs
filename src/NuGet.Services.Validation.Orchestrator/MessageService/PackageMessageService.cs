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
    public class PackageMessageService : MessageServiceConfiguration, IMessageService<Package>
    {
        private readonly ICoreMessageService _coreMessageService;
        private readonly ILogger<PackageMessageService> _logger;

        public PackageMessageService(
            ICoreMessageService coreMessageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<PackageMessageService> logger) : base(emailConfigurationAccessor)
        {
            _coreMessageService = coreMessageService ?? throw new ArgumentNullException(nameof(coreMessageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPublishedMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);

            await _coreMessageService.SendPackageAddedNoticeAsync(package, galleryPackageUrl, packageSupportUrl, _emailConfiguration.EmailSettingsUrl);
        }

        public async Task SendValidationFailedMessageAsync(Package package, PackageValidationSet validationSet)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));
            validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));

            var galleryPackageUrl = GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);

            await _coreMessageService.SendPackageValidationFailedNoticeAsync(package, validationSet, galleryPackageUrl, packageSupportUrl, _emailConfiguration.AnnouncementsUrl, _emailConfiguration.TwitterUrl);
        }

        public async Task SendValidationTakingTooLongMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            await _coreMessageService.SendValidationTakingTooLongNoticeAsync(package, GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion));
        }
    }
}
