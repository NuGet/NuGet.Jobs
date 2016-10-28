// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Services;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal abstract class SupportRequestsNotificationScheduledTask
      : IScheduledTask
    {

        protected SupportRequestsNotificationScheduledTask(
          ILoggerFactory loggerFactory,
          SupportRequestService supportRequestService,
          MessagingService messagingService)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (supportRequestService == null)
            {
                throw new ArgumentNullException(nameof(supportRequestService));
            }

            if (messagingService == null)
            {
                throw new ArgumentNullException(nameof(messagingService));
            }

            LoggerFactory = loggerFactory;
            MessagingService = messagingService;
            SupportRequestService = supportRequestService;
        }

        protected ILoggerFactory LoggerFactory { get; }
        protected MessagingService MessagingService { get; }
        protected SupportRequestService SupportRequestService { get; }

        public abstract Task Run();
    }
}
