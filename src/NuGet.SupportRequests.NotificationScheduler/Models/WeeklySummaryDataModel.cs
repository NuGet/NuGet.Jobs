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

            var unresolvedIssuesLastWeek = unresolvedIssues
                .Where(i => i.CreatedDate >= startDateUtcLastWeek && i.CreatedDate < referenceTime)
                .ToList();

            var unresolvedIssuesPriorWeek = unresolvedIssues
                .Where(i => i.CreatedDate >= startDateUtcPriorWeek && i.CreatedDate < startDateUtcLastWeek)
                .ToList();

            LastWeek = new SingleWeekSummary(unresolvedIssuesLastWeek);
            PriorWeek = new SingleWeekSummary(unresolvedIssuesPriorWeek);

            UnresolvedPercentageDelta = GetUnresolvedPercentageLastWeek() -
                                        GetUnresolvedPercentagePriorWeek();
        }

        public SingleWeekSummary LastWeek { get; }
        public SingleWeekSummary PriorWeek { get; }

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

        public double GetIssuesUnresolvedTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.UnresolvedCount, PriorWeek.UnresolvedCount);
        }
        public double UnresolvedPercentageDelta { get; }

        public double GetIssuesWaitingForCustomerTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.WaitingForCustomerCount, PriorWeek.WaitingForCustomerCount);
        }

        public double GetIssuesInProgressTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.InProgressCount, PriorWeek.InProgressCount);
        }

        public double GetUnresolvedPercentageLastWeek()
        {
            if (IssuesCreatedLastWeek == 0)
            {
                return LastWeek.UnresolvedCount;
            }

            return (double)LastWeek.UnresolvedCount / IssuesCreatedLastWeek;
        }
        public double GetUnresolvedPercentagePriorWeek()
        {
            if (IssuesCreatedPriorWeek == 0)
            {
                return PriorWeek.UnresolvedCount;
            }

            return (double)PriorWeek.UnresolvedCount / IssuesCreatedPriorWeek;
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

    internal class SingleWeekSummary
    {
        public SingleWeekSummary(IReadOnlyCollection<SupportRequest> unresolvedIssues)
        {
            UnresolvedCount = unresolvedIssues.Count;
            WaitingForCustomerCount = unresolvedIssues.Count(i =>
                i.IssueStatus == (int) IssueStatusKeys.WaitingForCustomer);
            InProgressCount = unresolvedIssues.Count(i =>
                i.IssueStatus == (int) IssueStatusKeys.Working);
        }

        public TimeSpan AverageTimeToResolution { get; set; }
        public int IssuesClosed { get; set; }
        public int IssuesCreated { get; set; }

        public int InProgressCount { get; }
        public int UnresolvedCount { get; }
        public int WaitingForCustomerCount { get; }
    }
}
