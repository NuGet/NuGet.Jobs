// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SupportRequests.NotificationScheduler
{
    internal class PagerDutyConfiguration
    {
        public PagerDutyConfiguration(string accountName, string apiKey)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException(nameof(accountName));
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            AccountName = accountName;
            ApiKey = apiKey;
        }

        public string ApiKey { get; }
        public string AccountName { get; }
    }
}
