// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class CoreMessageServiceConfiguration : ICoreMessageServiceConfiguration
    {
        public CoreMessageServiceConfiguration(EmailConfiguration emailConfiguration)
        {
            if (emailConfiguration == null)
            {
                throw new ArgumentNullException(nameof(emailConfiguration));
            }

            GalleryOwner = new MailAddress(emailConfiguration.GalleryOwner);
            GalleryNoReplyAddress = new MailAddress(emailConfiguration.GalleryNoReplyAddress);
        }

        public MailAddress GalleryOwner { get; set; }
        public MailAddress GalleryNoReplyAddress { get; set; }
    }
}
