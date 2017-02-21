// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.NotificationScheduler.Models;
using NuGet.SupportRequests.NotificationScheduler.Notifications;
using NuGet.SupportRequests.NotificationScheduler.Templates;

namespace NuGet.SupportRequests.NotificationScheduler.Tasks
{
    internal class WeeklySummaryNotificationTask
      : SupportRequestsNotificationScheduledTask<WeeklySummaryNotification>
    {
        private const string _argumentNameTargetEmailAddress = "TargetEmailAddress";

        private readonly string _targetEmailAddress;

        public WeeklySummaryNotificationTask(
          IDictionary<string, string> jobArgsDictionary,
          ILoggerFactory loggerFactory)
          : base(jobArgsDictionary, loggerFactory)
        {
            _targetEmailAddress = jobArgsDictionary[_argumentNameTargetEmailAddress];
        }

        protected override async Task<WeeklySummaryNotification> BuildNotification(
            SupportRequestRepository supportRequestRepository,
            DateTime referenceTime)
        {
            SingleWeekSummary lastWeekSummary;
            SingleWeekSummary priorWeekSummary;
            List<SupportRequest> unresolvedIssues;
            IDictionary<string, int> topSupportRequestReasonsLastWeek;

            var startDateUtcLastWeek = referenceTime.AddDays(-7);
            var startDateUtcPriorWeek = referenceTime.AddDays(-14);

            using (var connection = await supportRequestRepository.OpenConnectionAsync())
            {
                unresolvedIssues = await supportRequestRepository.GetUnresolvedIssues(connection);

                lastWeekSummary = await supportRequestRepository.GetSingleWeekSummary(
                    connection,
                    startDateUtcLastWeek,
                    referenceTime,
                    unresolvedIssues);

                priorWeekSummary = await supportRequestRepository.GetSingleWeekSummary(
                    connection,
                    startDateUtcPriorWeek,
                    startDateUtcLastWeek,
                    unresolvedIssues);

                topSupportRequestReasonsLastWeek = await supportRequestRepository.GetTopSupportRequestReasonsLastWeek(
                    connection,
                    startDateUtcLastWeek,
                    referenceTime);
            }

            return new WeeklySummaryNotification(
                referenceTime,
                unresolvedIssues,
                _targetEmailAddress,
                lastWeekSummary,
                priorWeekSummary,
                topSupportRequestReasonsLastWeek);
        }

        protected override string BuildNotificationBody(string template, WeeklySummaryNotification notification)
        {

            var newIssuesTrendPct = notification.GetNewIssuesTrendPct();
            var newIssuesTrendImg = HtmlSnippets.GetTrendImage(newIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var closedIssuesTrendPct = notification.GetIssuesClosedTrendPct();
            var closedIssuesTrendImg = HtmlSnippets.GetTrendImage(closedIssuesTrendPct, Images.UpArrowGreen, Images.DownArrowRed);

            var unresolvedIssuesTrendPct = notification.GetIssuesUnresolvedTrendPct();
            var unresolvedIssuesTrendImg = HtmlSnippets.GetTrendImage(unresolvedIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var waitingIssuesTrendPct = notification.GetIssuesWaitingForCustomerTrendPct();
            var waitingIssuesTrendImg = HtmlSnippets.GetTrendImage(waitingIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var workingIssuesTrendPct = notification.GetIssuesInProgressTrendPct();
            var workingIssuesTrendImg = HtmlSnippets.GetTrendImage(workingIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var slaUnresolvedPctDeltaImg = HtmlSnippets.GetTrendImage(notification.UnresolvedPercentageDelta, Images.UpArrowRed, Images.DownArrowGreen);

            var slaAverageTimeToResolutionDelta = notification.LastWeek.AverageTimeToResolution - notification.PriorWeek.AverageTimeToResolution;
            var slaAverageTimeToResolutionImg = HtmlSnippets.GetTrendImage(slaAverageTimeToResolutionDelta.Ticks, Images.UpArrowRed, Images.DownArrowGreen);

            var result = template
                .Replace(HtmlPlaceholders.ReportDate, notification.ReferenceTime.ToString("dd/MM/yy"))

                // new support requests
                .Replace(HtmlPlaceholders.NewIssueCountPriorWeek, notification.PriorWeek.CreatedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.NewIssueCountLastWeek, notification.LastWeek.CreatedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.NewIssueCountDeltaValue, HtmlSnippets.GetTrendPercentageString(newIssuesTrendPct))
                .Replace(HtmlPlaceholders.NewIssueCountDeltaImage, newIssuesTrendImg)

                // closed support requests
                .Replace(HtmlPlaceholders.ClosedIssueCountPriorWeek, notification.PriorWeek.ClosedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.ClosedIssueCountLastWeek, notification.LastWeek.ClosedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.ClosedIssueCountDeltaValue, HtmlSnippets.GetTrendPercentageString(closedIssuesTrendPct))
                .Replace(HtmlPlaceholders.ClosedIssueCountDeltaImage, closedIssuesTrendImg)

                // unresolved support requests
                .Replace(HtmlPlaceholders.UnresolvedIssueCountPriorWeek, notification.PriorWeek.UnresolvedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountLastWeek, notification.LastWeek.UnresolvedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountDeltaValue, HtmlSnippets.GetTrendPercentageString(unresolvedIssuesTrendPct))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountDeltaImage, unresolvedIssuesTrendImg)

                // waiting on customer
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountPriorWeek, notification.PriorWeek.WaitingForCustomerCount.ToString("N0"))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountLastWeek, notification.LastWeek.WaitingForCustomerCount.ToString("N0"))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountDeltaValue, HtmlSnippets.GetTrendPercentageString(waitingIssuesTrendPct))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountDeltaImage, waitingIssuesTrendImg)

                // in progress
                .Replace(HtmlPlaceholders.InProgressIssueCountPriorWeek, notification.PriorWeek.InProgressCount.ToString("N0"))
                .Replace(HtmlPlaceholders.InProgressIssueCountLastWeek, notification.LastWeek.InProgressCount.ToString("N0"))
                .Replace(HtmlPlaceholders.InProgressIssueCountDeltaValue, HtmlSnippets.GetTrendPercentageString(workingIssuesTrendPct))
                .Replace(HtmlPlaceholders.InProgressIssueCountDeltaImage, workingIssuesTrendImg)

                // SLA - unresolved pct
                .Replace(HtmlPlaceholders.UnresolvedPercentagePriorWeek, notification.PriorWeek.GetUnresolvedPercentage().ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageLastWeek, notification.LastWeek.GetUnresolvedPercentage().ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageDeltaValue, notification.UnresolvedPercentageDelta.ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageDeltaImage, slaUnresolvedPctDeltaImg)

                // SLA - average time to resolution
                .Replace(HtmlPlaceholders.AvgTimeToResolutionPriorWeek, notification.PriorWeek.AverageTimeToResolution.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionLastWeek, notification.LastWeek.AverageTimeToResolution.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionDeltaValue, slaAverageTimeToResolutionDelta.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionDeltaImage, slaAverageTimeToResolutionImg);

            // Top 3 support request reasons last week
            result = FillInTopSupportRequestReasons(notification.TopSupportRequestReasonsLastWeek, result);

            // who has open issues assigned
            result = FillInUnresolvedIssuesByAssignedTo(notification, result);

            return result;
        }

        private static string FillInUnresolvedIssuesByAssignedTo(
            WeeklySummaryNotification notification,
            string result)
        {
            var oncallStatusReportBuilder = new StringBuilder();

            foreach (var unresolvedIssueGroup in notification.UnresolvedIssues.GroupBy(i => i.AdminGalleryUsername).OrderByDescending(i => i.Count()))
            {
                var status = string.Empty;
                foreach (var unresolvedIssueStatusGroup in unresolvedIssueGroup.GroupBy(i => i.IssueStatus).OrderByDescending(i => i.Count()))
                {
                    if (!string.IsNullOrEmpty(status))
                    {
                        status += HtmlSnippets.OnCallStatusItemSeparator;
                    }

                    var issueCount = unresolvedIssueStatusGroup.Count();

                    if (issueCount > 1)
                    {
                        status += string.Format(
                            HtmlSnippets.OnCallStatusItemTemplate,
                            unresolvedIssueStatusGroup.Count(),
                            Enum.GetName(typeof(IssueStatusKeys), unresolvedIssueStatusGroup.Key));
                    }
                    else
                    {
                        status += string.Format(
                            HtmlSnippets.OnCallStatusItemTemplateSingle,
                            unresolvedIssueStatusGroup.Count(),
                            Enum.GetName(typeof(IssueStatusKeys), unresolvedIssueStatusGroup.Key));
                    }
                }

                oncallStatusReportBuilder.AppendFormat(HtmlSnippets.OnCallStatusTemplate, unresolvedIssueGroup.Key, status.TrimStart(','));
            }

            result = result.Replace(HtmlPlaceholders.OnCallStatusReport, oncallStatusReportBuilder.ToString());

            return result;
        }

        private static string FillInTopSupportRequestReasons(
            IDictionary<string, int> reasons,
            string result)
        {
            const string singleIssue = "1 issue";
            const string position1Placeholder = "$$INFO_1W-AGO_TOP_REASON_1$$";
            const string position1CountPlaceholder = "$$INFO_1W-AGO_TOP_REASON_1_COUNT$$";
            const string position2Placeholder = "$$INFO_1W-AGO_TOP_REASON_2$$";
            const string position2CountPlaceholder = "$$INFO_1W-AGO_TOP_REASON_2_COUNT$$";
            const string position3Placeholder = "$$INFO_1W-AGO_TOP_REASON_3$$";
            const string position3CountPlaceholder = "$$INFO_1W-AGO_TOP_REASON_3_COUNT$$";

            var count = reasons.Count;
            if (count >= 1)
            {
                var position1 = reasons.ElementAt(0);
                result = result
                    .Replace(position1Placeholder, position1.Key)
                    .Replace(position1CountPlaceholder, position1.Value == 1 ? singleIssue : $"{position1.Value} issues");

                if (count >= 2)
                {
                    var position2 = reasons.ElementAt(1);
                    result = result
                        .Replace(position2Placeholder, position2.Key)
                        .Replace(position2CountPlaceholder, position2.Value == 1 ? singleIssue : $"{position2.Value} issues");

                    if (count >= 3)
                    {
                        var position3 = reasons.ElementAt(2);
                        result = result
                            .Replace(position3Placeholder, position3.Key)
                            .Replace(position3CountPlaceholder, position3.Value == 1 ? singleIssue : $"{position3.Value} issues");
                    }
                    else
                    {
                        result = result
                            .Replace(position3Placeholder, HtmlSnippets.EmptyResultSpan)
                            .Replace(position3CountPlaceholder, string.Empty);
                    }
                }
                else
                {
                    result = result
                        .Replace(position2Placeholder, HtmlSnippets.EmptyResultSpan)
                        .Replace(position2CountPlaceholder, string.Empty)
                        .Replace(position3Placeholder, HtmlSnippets.EmptyResultSpan)
                        .Replace(position3CountPlaceholder, string.Empty);
                }
            }
            else
            {
                result = result
                    .Replace(position1Placeholder, HtmlSnippets.EmptyResultSpan)
                    .Replace(position1CountPlaceholder, string.Empty)
                    .Replace(position2Placeholder, HtmlSnippets.EmptyResultSpan)
                    .Replace(position2CountPlaceholder, string.Empty)
                    .Replace(position3Placeholder, HtmlSnippets.EmptyResultSpan)
                    .Replace(position3CountPlaceholder, string.Empty);
            }

            return result;
        }

    }
}
