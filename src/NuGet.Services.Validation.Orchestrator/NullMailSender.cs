// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// <see cref="IMailSender"/> implementation that does nothing for the case when email is not configured
    /// </summary>
    public class NullMailSender : IMailSender
    {
        public void Send(string fromAddress, string toAddress, string subject, string markdownBody)
        {
        }

        public void Send(MailAddress fromAddress, MailAddress toAddress, string subject, string markdownBody)
        {
        }

        public void Send(MailMessage mailMessage)
        {
        }
    }
}
