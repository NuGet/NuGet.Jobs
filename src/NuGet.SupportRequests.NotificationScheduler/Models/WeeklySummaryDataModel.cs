// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.SupportRequests.NotificationScheduler.Models
{
    internal class WeeklySummaryDataModel
    {
        public WeeklySummaryDataModel(
            DateTime referenceTime,
            List<SupportRequest> unresolvedIssues)
        {
            if (referenceTime == null)
            {
                throw new ArgumentNullException(nameof(referenceTime));
            }
            if (unresolvedIssues == null)
            {
                throw new ArgumentNullException(nameof(unresolvedIssues));
            }

            ReferenceTime = referenceTime;
            UnresolvedIssues = unresolvedIssues;
            TopSupportRequestReasonsLastWeek = new Dictionary<string, int>();

            var startDateUtcLastWeek = referenceTime.AddDays(-7);
            var startDateUtcPriorWeek = referenceTime.AddDays(-14);

            // Get number of issues created last week
            IssuesUnresolvedLastWeek = unresolvedIssues.Count(i =>
                    i.CreatedDate >= startDateUtcLastWeek
                    && i.CreatedDate < referenceTime);

            // Get number of issues created in prior week
            IssuesUnresolvedPriorWeek = unresolvedIssues.Count(i =>
                    i.CreatedDate >= startDateUtcPriorWeek
                    && i.CreatedDate < startDateUtcLastWeek);

            // Get number of issues that are still waiting for customer and created in the last 7 days
            IssuesWaitingForCustomerLastWeek = unresolvedIssues.Count(i =>
                    i.IssueStatus == (int)IssueStatusKeys.WaitingForCustomer
                    && i.CreatedDate >= startDateUtcLastWeek
                    && i.CreatedDate < referenceTime);

            // Get number of issues that are still waiting for customer and created in prior week
            IssuesWaitingForCustomerPriorWeek = unresolvedIssues.Count(i =>
                    i.IssueStatus == (int)IssueStatusKeys.WaitingForCustomer
                    && i.CreatedDate >= startDateUtcPriorWeek
                    && i.CreatedDate < startDateUtcLastWeek);

            // Get number of issues that are still being handled by support agent and created in the last 7 days
            IssuesInProgressLastWeek = unresolvedIssues.Count(i =>
                    i.IssueStatus == (int)IssueStatusKeys.Working
                    && i.CreatedDate >= startDateUtcLastWeek
                    && i.CreatedDate < referenceTime);

            // Get number of issues that are still being handled by support agent and created in prior week
            IssuesInProgressPriorWeek = unresolvedIssues.Count(i =>
                    i.IssueStatus == (int)IssueStatusKeys.Working
                    && i.CreatedDate >= startDateUtcPriorWeek
                    && i.CreatedDate < startDateUtcLastWeek);

            UnresolvedPercentageDelta = GetUnresolvedPercentageLastWeek() -
                                        GetUnresolvedPercentagePriorWeek();
        }

        public DateTime ReferenceTime { get; }

        public List<SupportRequest> UnresolvedIssues { get; }

        public TimeSpan AverageTimeToResolutionPriorWeek { get; set; }
        public TimeSpan AverageTimeToResolutionLastWeek { get; set; }

        public int IssuesClosedLastWeek { get; set; }
        public int IssuesClosedPriorWeek { get; set; }
        public double GetIssuesClosedTrendPct()
        {
            return GetDeltaPercentageSafe(IssuesClosedLastWeek, IssuesClosedPriorWeek);
        }

        public int IssuesCreatedLastWeek { get; set; }
        public int IssuesCreatedPriorWeek { get; set; }
        public double GetNewIssuesTrendPct()
        {
            return GetDeltaPercentageSafe(IssuesCreatedLastWeek, IssuesCreatedPriorWeek);
        }

        public int IssuesUnresolvedLastWeek { get; }
        public int IssuesUnresolvedPriorWeek { get; }
        public double GetIssuesUnresolvedTrendPct()
        {
            return GetDeltaPercentageSafe(IssuesUnresolvedLastWeek, IssuesUnresolvedPriorWeek);
        }
        public double UnresolvedPercentageDelta { get; }

        public int IssuesWaitingForCustomerLastWeek { get; }
        public int IssuesWaitingForCustomerPriorWeek { get; }
        public double GetIssuesWaitingForCustomerTrendPct()
        {
            return GetDeltaPercentageSafe(IssuesWaitingForCustomerLastWeek, IssuesWaitingForCustomerPriorWeek);
        }

        public int IssuesInProgressLastWeek { get; }
        public int IssuesInProgressPriorWeek { get; }
        public double GetIssuesInProgressTrendPct()
        {
            return GetDeltaPercentageSafe(IssuesInProgressLastWeek, IssuesInProgressPriorWeek);
        }

        public double GetUnresolvedPercentageLastWeek()
        {
            if (IssuesCreatedLastWeek == 0)
            {
                return IssuesUnresolvedLastWeek;
            }

            return (double)IssuesUnresolvedLastWeek / IssuesCreatedLastWeek;
        }
        public double GetUnresolvedPercentagePriorWeek()
        {
            if (IssuesCreatedPriorWeek == 0)
            {
                return IssuesUnresolvedPriorWeek;
            }

            return (double)IssuesUnresolvedPriorWeek / IssuesCreatedPriorWeek;
        }

        public IDictionary<string, int> TopSupportRequestReasonsLastWeek { get; set; }

        private static double GetDeltaPercentageSafe(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                if (numerator > 0)
                {
                    return double.PositiveInfinity;
                }
                else if (numerator < 0)
                {
                    return double.NegativeInfinity;
                }

                return double.NaN;
            }
            else
            {
                return (double)(numerator - denominator) / denominator;
            }
        }
    }
}
