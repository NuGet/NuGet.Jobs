// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SupportRequests.NotificationScheduler.Models
{
    internal class DailySummaryDataModel
    {
        public DailySummaryDataModel(
            DateTime referenceTime,
            List<SupportRequest> unresolvedIssues,
            string onCallEmailAddress)
        {
            if (referenceTime == null)
            {
                throw new ArgumentNullException(nameof(referenceTime));
            }

            if (unresolvedIssues == null)
            {
                throw new ArgumentNullException(nameof(unresolvedIssues));
            }

            if (string.IsNullOrEmpty(onCallEmailAddress))
            {
                throw new ArgumentNullException(nameof(onCallEmailAddress));
            }

            ReferenceTime = referenceTime;
            UnresolvedIssues = unresolvedIssues;
            OnCallEmailAddress = onCallEmailAddress;
        }

        public DateTime ReferenceTime { get; }

        public List<SupportRequest> UnresolvedIssues { get; }

        public string OnCallEmailAddress { get; }
    }
}