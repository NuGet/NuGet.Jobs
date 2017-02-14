// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Gallery.CredentialExpiration.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace Gallery.CredentialExpiration
{
    public class Job : JobBase
    {
        private const int DefaultCommandTimeout = 1800; // 30 minutes max

        private readonly ConcurrentDictionary<string, DateTimeOffset> _contactedUsers = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly string _cursorFile = "cursor.json";

        private bool _whatIf = false;

        private string _galleryBrand;
        private string _galleryAccountUrl;

        private SqlConnectionStringBuilder _galleryDatabase;

        private string _mailFrom;
        private SmtpClient _smtpClient;

        private int _allowEmailResendAfterDays = 7;
        private int _warnDaysBeforeExpiration = 10;

        private ILogger _logger;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                _logger = loggerFactory.CreateLogger<Job>();

                _whatIf = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.WhatIf);

                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryDatabase);
                _galleryDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                _galleryBrand = JobConfigurationManager.GetArgument(jobArgsDictionary, MyJobArgumentNames.GalleryBrand);
                _galleryAccountUrl = JobConfigurationManager.GetArgument(jobArgsDictionary, MyJobArgumentNames.GalleryAccountUrl);

                _mailFrom = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.MailFrom);

                var smtpConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SmtpUri);
                var smtpUri = new SmtpUri(new Uri(smtpConnectionString));
                _smtpClient = CreateSmtpClient(smtpUri);

                var warnDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, MyJobArgumentNames.WarnDaysBeforeExpiration);
                _warnDaysBeforeExpiration = warnDays.HasValue ? warnDays.Value : _warnDaysBeforeExpiration;

                var resendDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, MyJobArgumentNames.AllowEmailResendAfterDays);
                _allowEmailResendAfterDays = resendDays.HasValue ? resendDays.Value : _allowEmailResendAfterDays;
            }
            catch (Exception exception)
            {
                _logger.LogCritical("Failed to initialize job! {Exception}", exception);

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                List<ExpiredCredentialData> expiredCredentials = null;

                // Who did we contact before?
                if (File.Exists(_cursorFile))
                {
                    // Load from cursor
                    var contactedUsers = JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(
                        File.ReadAllText(_cursorFile));

                    // Clean older entries (contacted in last _resendEmailAfterDays)
                    var referenceDate = DateTimeOffset.UtcNow.AddDays(-1 * _allowEmailResendAfterDays);
                    foreach (var kvp in contactedUsers.Where(kvp => kvp.Value >= referenceDate))
                    {
                        _contactedUsers.AddOrUpdate(kvp.Key, kvp.Value, (s, offset) => kvp.Value);
                    }
                }

                // Connect to database
                using (var galleryConnection = await _galleryDatabase.ConnectTo())
                {
                    // Fetch credentials that expire in _warnDaysBeforeExpiration days 
                    // + the user's e-mail address
                    _logger.LogInformation("Retrieving expired credentials from {InitialCatalog}...",
                        _galleryDatabase.InitialCatalog);

                    expiredCredentials = (await galleryConnection.QueryWithRetryAsync<ExpiredCredentialData>(
                        string.Format(Strings.GetExpiredCredentialsQuery, _warnDaysBeforeExpiration),
                        maxRetries: 3,
                        commandTimeout: DefaultCommandTimeout)).ToList();

                    _logger.LogInformation("Retrieved {ExpiredCredentials} expired credentials.",
                        expiredCredentials.Count);
                }

                // Add default description for non-scoped API keys
                expiredCredentials
                    .Where(cred => (string.IsNullOrEmpty(cred.Description) || cred.IsNonScopedApiKey()))
                    .ToList()
                    .ForEach(ecd => ecd.Description = Constants.NonScopedApiKeyDescription);

                // Group credentials for each user
                var userToExpiredCredsMapping = expiredCredentials
                    .GroupBy(x => x.Username)
                    .ToDictionary(user => user.Key, value => value.ToList());

                // Handle expiring credentials
                var jobRunTime = DateTimeOffset.UtcNow;
                foreach (var userCredMapping in userToExpiredCredsMapping)
                {
                    var username = userCredMapping.Key;
                    var credentialList = userCredMapping.Value;

                    // Split credentials into two lists: Expired and Expiring to aggregate messages
                    var expiringCredendtialList = credentialList
                        .Where(x => (x.Expires - jobRunTime).TotalDays > 0)
                        .ToList();
                    var expiredCredentialList = credentialList
                        .Where(x => (x.Expires - jobRunTime).TotalDays <= 0)
                        .ToList();

                    if (!_contactedUsers.ContainsKey(username))
                    {
                        // send expiring API keys email notification
                        await HandleExpiredCredentialEmail(username, expiringCredendtialList, jobRunTime, expired: false);

                        // send expired API keys email notification
                        await HandleExpiredCredentialEmail(username, expiredCredentialList, jobRunTime, expired: true);

                        _logger.LogInformation("Handled expired/expiring credential for user {Username}.", username);
                        _contactedUsers.AddOrUpdate(username, jobRunTime, (s, offset) => jobRunTime);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping expired credential for user {Username} - already handled in last {days}.",
                            username, _allowEmailResendAfterDays);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Job run failed! {Exception}", ex);

                return false;
            }
            finally
            {
                // Make sure we know who has been contacted today, so they do not get double
                // e-mail notifications.
                File.WriteAllText(_cursorFile, JsonConvert.SerializeObject(_contactedUsers));
            }

            return true;
        }

        private async Task HandleExpiredCredentialEmail(string username, List<ExpiredCredentialData> credentialList, DateTimeOffset jobRunTime, bool expired)
        {
            if (credentialList == null || credentialList.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Handling {Expired} credential(s) for user {Username} (Keys: {Descriptions})...",
                expired ? "expired" : "expiring",
                username,
                string.Join(", ", credentialList.Select(x => x.Description).ToList()));

            // Build message
            var userEmail = credentialList.FirstOrDefault().EmailAddress;
            var mailMessage = new MailMessage(_mailFrom, userEmail);

            var apiKeyExpiryMessageList = credentialList
                .Select(x => BuildApiKeyExpiryMessage(x.Description, x.Expires, jobRunTime))
                .ToList();

            var apiKeyExpiryMessage = string.Join(Environment.NewLine, apiKeyExpiryMessageList);
            // Build email body
            if (expired)
            {
                mailMessage.Subject = string.Format(Strings.ExpiredEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiredEmailBody, username, _galleryBrand, apiKeyExpiryMessage, _galleryAccountUrl);
            }
            else
            {
                mailMessage.Subject = string.Format(Strings.ExpiringEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiringEmailBody, username, _galleryBrand, apiKeyExpiryMessage, _galleryAccountUrl);
            }

            // Send email
            try
            {
                if (!_whatIf) // if WhatIf is passed, we will not send e-mails (e.g. dev/int don't have to annoy users)
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }
            }
            catch (SmtpFailedRecipientException ex)
            {
                _logger.LogWarning("Failed to handle expired credential for user {Username} - recipient failed! {Exception}", username, ex);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Failed to handle expired credential for user {Username}! {Exception}", username, ex);

                throw;
            }
        }

        private static string BuildApiKeyExpiryMessage(string description, DateTimeOffset expiry, DateTimeOffset currentTime)
        {
            var expiryInDays = (expiry - currentTime).TotalDays;
            var message = expiryInDays < 0
                ? string.Format(Strings.ApiKeyExpired, description)
                : string.Format(Strings.ApiKeyExpiring, description, (int)expiryInDays);

            // \u2022 - Unicode for bullet point.
            return "\u2022 "+ message + Environment.NewLine;
        }

        private SmtpClient CreateSmtpClient(SmtpUri smtpUri)
        {
            var smtpClient = new SmtpClient(smtpUri.Host, smtpUri.Port)
            {
                EnableSsl = smtpUri.Secure
            };

            if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(
                    smtpUri.UserName,
                    smtpUri.Password);
            }

            return smtpClient;
        }
    }
}