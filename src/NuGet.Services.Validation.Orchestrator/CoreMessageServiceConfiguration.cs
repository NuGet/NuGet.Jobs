﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class CoreMessageServiceConfiguration : ICoreMessageServiceConfiguration
    {
        public CoreMessageServiceConfiguration(IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor)
        {
            if (emailConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(emailConfigurationAccessor));
            }

            var emailConfiguration = emailConfigurationAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(emailConfigurationAccessor));
            GalleryOwner = new MailAddress(emailConfiguration.GalleryOwner 
                ?? throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(emailConfiguration.GalleryOwner)} property cannot be null", nameof(emailConfigurationAccessor)));
            GalleryNoReplyAddress = new MailAddress(emailConfiguration.GalleryNoReplyAddress
                ?? throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(emailConfiguration.GalleryNoReplyAddress)} property cannot be null", nameof(emailConfigurationAccessor)));
        }

        public MailAddress GalleryOwner { get; set; }
        public MailAddress GalleryNoReplyAddress { get; set; }
    }
}
