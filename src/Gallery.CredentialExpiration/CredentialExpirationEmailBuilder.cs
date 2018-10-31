// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Gallery.CredentialExpiration.Models;
using NuGet.Services.Messaging.Email;

namespace Gallery.CredentialExpiration
{
    public class CredentialExpirationEmailBuilder : MarkdownEmailBuilder
    {
        public CredentialExpirationEmailBuilder(
            InitializationConfiguration initializationConfiguration,
            MailAddress sender,
            string username,
            List<ExpiredCredentialData> credentials,
            DateTimeOffset jobRunTime,
            bool areCredentialsExpired)
        {
            InitializationConfiguration = initializationConfiguration;

            Sender = sender;
            Credentials = credentials;
            JobRunTime = jobRunTime;
            AreCredentialsExpired = areCredentialsExpired;

            Username = username;
            var userEmail = credentials.FirstOrDefault().EmailAddress;
            UserAddress = new MailAddress(userEmail, Username);
        }

        public InitializationConfiguration InitializationConfiguration { get; }
        
        public string Username { get; }
        public MailAddress UserAddress { get; }
        public List<ExpiredCredentialData> Credentials { get; }
        public DateTimeOffset JobRunTime { get; }
        public bool AreCredentialsExpired { get; }

        public override MailAddress Sender { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new MailAddress[] { UserAddress });
        }

        public override string GetSubject()
        {
            if (AreCredentialsExpired)
            {
                return string.Format(Strings.ExpiredEmailSubject, InitializationConfiguration.GalleryBrand);
            }
            else
            {
                return string.Format(Strings.ExpiringEmailSubject, InitializationConfiguration.GalleryBrand);
            }
        }

        protected override string GetMarkdownBody()
        {
            var apiKeyExpiryMessageList = Credentials
                .Select(x => BuildApiKeyExpiryMessage(x.Description, x.Expires, JobRunTime))
                .ToList();

            var apiKeyExpiryMessage = string.Join(Environment.NewLine, apiKeyExpiryMessageList);
            if (AreCredentialsExpired)
            {
                return string.Format(Strings.ExpiredEmailBody, Username, InitializationConfiguration.GalleryBrand, apiKeyExpiryMessage, InitializationConfiguration.GalleryAccountUrl);
            }
            else
            {
                return string.Format(Strings.ExpiredEmailBody, Username, InitializationConfiguration.GalleryBrand, apiKeyExpiryMessage, InitializationConfiguration.GalleryAccountUrl);
            }
        }

        private static string BuildApiKeyExpiryMessage(string description, DateTimeOffset expiry, DateTimeOffset currentTime)
        {
            var expiryInDays = (expiry - currentTime).TotalDays;
            var message = expiryInDays < 0
                ? string.Format(Strings.ApiKeyExpired, description)
                : string.Format(Strings.ApiKeyExpiring, description, (int)expiryInDays);

            // \u2022 - Unicode for bullet point.
            return "\u2022 " + message + Environment.NewLine;
        }
    }
}
