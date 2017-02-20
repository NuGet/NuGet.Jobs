// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal class OnCallDailyNotificationTask
      : SupportRequestsNotificationScheduledTask
    {
        public OnCallDailyNotificationTask(
            IDictionary<string, string> jobArgsDictionary,
            ILoggerFactory loggerFactory)
          : base(jobArgsDictionary, loggerFactory)
        {
        }

        public override async Task RunAsync()
        {
            var referenceTime = DateTime.UtcNow.Date;
            var dataModel = await SupportRequestService.GetDailySummaryDataAsync(referenceTime);

            MessagingService.SendDailySummaryNotification(dataModel);
        }
    }
}
