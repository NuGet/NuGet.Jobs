// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Messaging.Email;

namespace NuGet.SupportRequests.Notifications
{
    public class SupportRequestNotificationEmailBuilder : MarkdownEmailBuilder
    {
        public SupportRequestNotificationEmailBuilder(
            string subject,
            string body,
            string targetEmailAddress)
        {
            _subject = subject;
            _body = body;
            _targetAddress = new MailAddress(targetEmailAddress);
        }

        public static MailAddress NoReplyAddress = new MailAddress("NuGet Gallery <noreply@nuget.org>");
        public override MailAddress Sender => NoReplyAddress;

        private MailAddress _targetAddress;
        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(to: new[] { _targetAddress });
        }

        private readonly string _subject;
        public override string GetSubject()
        {
            return _subject;
        }

        private readonly string _body;
        protected override string GetMarkdownBody()
        {
            return _body;
        }
    }
}
