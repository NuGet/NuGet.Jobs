// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Services;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal class OnCallDailyNotificationTask
      : SupportRequestsNotificationScheduledTask
    {
        public OnCallDailyNotificationTask(
          ILoggerFactory loggerFactory,
          SupportRequestService supportRequestService,
          MessagingService messagingService)
          : base(loggerFactory, supportRequestService, messagingService)
        {
        }

        public override async Task Run()
        {
            var referenceTime = DateTime.UtcNow.Date;
            var dataModel = await SupportRequestService.GetDailySummaryDataAsync(referenceTime);

            MessagingService.SendDailySummaryNotification(dataModel);
        }
    }
}
