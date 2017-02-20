// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Models;
using NuGet.SupportRequests.NotificationScheduler.Services;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal abstract class SupportRequestsNotificationScheduledTask
      : IScheduledTask
    {
        private const string _argumentNamePagerDutyAccountName = "PagerDutyAccountName";
        private const string _argumentNamePagerDutyApiKey = "PagerDutyApiKey";
        private const string _argumentNameTargetEmailAddress = "TargetEmailAddress";

        protected SupportRequestsNotificationScheduledTask(
          IDictionary<string, string> jobArgsDictionary,
          ILoggerFactory loggerFactory)
        {
            if (jobArgsDictionary == null)
            {
                throw new ArgumentNullException(nameof(jobArgsDictionary));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            LoggerFactory = loggerFactory;
            MessagingService = CreateMessagingService(jobArgsDictionary);
            SupportRequestService = CreateSupportRequestsService(jobArgsDictionary);
        }

        protected ILoggerFactory LoggerFactory { get; }
        protected MessagingService MessagingService { get; }
        protected SupportRequestService SupportRequestService { get; }

        public abstract Task RunAsync();

        private SupportRequestService CreateSupportRequestsService(IDictionary<string, string> argsDictionary)
        {
            var databaseConnectionString = argsDictionary[JobArgumentNames.SourceDatabase];
            var sourceDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
            var supportRequestRepository = new SupportRequestRepository(LoggerFactory, sourceDatabase);

            var pagerDutyConfiguration = new PagerDutyConfiguration(
                argsDictionary[_argumentNamePagerDutyAccountName],
                argsDictionary[_argumentNamePagerDutyApiKey]
            );

            return new SupportRequestService(supportRequestRepository, pagerDutyConfiguration);
        }

        private MessagingService CreateMessagingService(IDictionary<string, string> argsDictionary)
        {
            var targetEmailAddress = argsDictionary[_argumentNameTargetEmailAddress];
            var smtpUri = argsDictionary[JobArgumentNames.SmtpUri];

            return new MessagingService(LoggerFactory, smtpUri, targetEmailAddress);
        }
    }
}
