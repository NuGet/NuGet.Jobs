// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Services;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal class WeeklySummaryNotificationTask
      : SupportRequestsNotificationScheduledTask
    {
        public WeeklySummaryNotificationTask(
          ILoggerFactory loggerFactory,
          SupportRequestService supportRequestService,
          MessagingService messagingService)
          : base(loggerFactory, supportRequestService, messagingService)
        {
        }

        public override async Task Run()
        {
            var referenceTime = DateTime.UtcNow.Date;

            // 1. Collect aggregates (# open, # closed, # new issues this week, # closed issues this week)
            var dataModel = await SupportRequestService.GetWeeklySummaryDataAsync(referenceTime);

            // 2. Build and send the message
            MessagingService.SendWeeklySummaryNotification(dataModel);
        }
    }
}
