// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.Notifications.Models;
using NuGet.SupportRequests.Notifications.Notifications;
using NuGet.SupportRequests.Notifications.Templates;

namespace NuGet.SupportRequests.Notifications.Tasks
{
    internal class OnCallDailyNotificationTask
      : SupportRequestsNotificationScheduledTask<OnCallDailyNotification>
    {
        private const string _argumentNamePagerDutyAccountName = "PagerDutyAccountName";
        private const string _argumentNamePagerDutyApiKey = "PagerDutyApiKey";
        private const string _targetEmailAddressFormat = "{0}@microsoft.com";
        private readonly PagerDutyClient _pagerDutyClient;

        public OnCallDailyNotificationTask(
            IDictionary<string, string> jobArgsDictionary,
            ILoggerFactory loggerFactory)
          : base(jobArgsDictionary, loggerFactory)
        {
            var pagerDutyConfiguration = new PagerDutyConfiguration(
                jobArgsDictionary[_argumentNamePagerDutyAccountName],
                jobArgsDictionary[_argumentNamePagerDutyApiKey]
            );

            _pagerDutyClient = new PagerDutyClient(pagerDutyConfiguration);
        }

        protected override async Task<OnCallDailyNotification> BuildNotification(
            SupportRequestRepository supportRequestRepository,
            DateTime referenceTime)
        {
            var onCallAlias = await _pagerDutyClient.GetPrimaryOnCallAsync();
            var targetEmailAddress = string.Format(_targetEmailAddressFormat, onCallAlias);

            List<SupportRequest> unresolvedIssues;
            using (var connection = await supportRequestRepository.OpenConnectionAsync())
            {
                unresolvedIssues = await supportRequestRepository.GetUnresolvedIssues(connection, onCallAlias);
            }

            return new OnCallDailyNotification(referenceTime, unresolvedIssues, targetEmailAddress);
        }

        protected override string BuildNotificationBody(string template, OnCallDailyNotification notification)
        {
            var result = template;
            var referenceTimeDisplay = notification.ReferenceTime.ToString("dd/MM/yy");

            if (notification.UnresolvedIssues.Any())
            {
                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.New),
                    result, HtmlPlaceholders.NewIssues);

                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.Working),
                    result, HtmlPlaceholders.WorkingIssues);

                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.WaitingForCustomer),
                    result, HtmlPlaceholders.WaitingForCustomerIssues);
            }
            else
            {
                result = result
                    .Replace(HtmlPlaceholders.NewIssues,
                        $"<tr class=\"border-trim\"><td colspan=\"4\">No new issues reported on {referenceTimeDisplay}</td></tr>")
                    .Replace(HtmlPlaceholders.WorkingIssues,
                        $"<tr class=\"border-trim\"><td colspan=\"4\">No issues in progress on {referenceTimeDisplay}</td></tr>")
                    .Replace(HtmlPlaceholders.WaitingForCustomerIssues,
                        $"<tr class=\"border-trim\"><td colspan=\"4\">No issues waiting for customer on {referenceTimeDisplay}</td></tr>");
            }

            result = result.Replace(HtmlPlaceholders.ReportDate, referenceTimeDisplay);

            return result;
        }

        private static string InjectIssueStatus(IEnumerable<SupportRequest> issues, string result, string issuePlaceholder)
        {
            if (issuePlaceholder == null)
            {
                throw new ArgumentNullException(nameof(issuePlaceholder));
            }

            var newIssuesStringBuilder = new StringBuilder();
            foreach (var issuesByCreator in issues
                .OrderBy(i => i.CreatedDate)
                .GroupBy(i => i.CreatedBy)
                .OrderBy(i => i.Key))
            {
                foreach (var supportRequest in issuesByCreator)
                {
                    var issueHtml = BuildIssueHtml(issuesByCreator.Key, supportRequest);

                    newIssuesStringBuilder.Append(issueHtml);
                }
            }

            result = result.Replace(
                issuePlaceholder,
                newIssuesStringBuilder.ToString());
            return result;
        }

        private static string BuildIssueHtml(string createdByUserName, SupportRequest supportRequest)
        {
            var issueCreatorProfileLink = CreateUserProfileLink(createdByUserName);

            var issueHtmlTemplate = HtmlSnippets.IssueTemplate
                .Replace(HtmlPlaceholders.From,
                    $"[{supportRequest.CreatedDate:MM/dd hh:mm tt} UTC] - {issueCreatorProfileLink}");

            var issueHtml = issueHtmlTemplate.Replace(HtmlPlaceholders.Reason, supportRequest.Reason);

            if (!string.IsNullOrEmpty(supportRequest.PackageId))
            {
                var packageLinkHtml = HtmlSnippets.PackageLinkTemplate
                    .Replace(HtmlPlaceholders.PackageId, supportRequest.PackageId);

                if (!string.IsNullOrEmpty(supportRequest.PackageVersion))
                {
                    packageLinkHtml = packageLinkHtml
                        .Replace(HtmlPlaceholders.PackageVersion, supportRequest.PackageVersion)
                        .Replace(HtmlPlaceholders.PackageVersionLabel, $" v{supportRequest.PackageVersion}");
                }
                else
                {
                    packageLinkHtml = packageLinkHtml
                        .Replace(HtmlPlaceholders.PackageVersion, string.Empty)
                        .Replace(HtmlPlaceholders.PackageVersionLabel, string.Empty);
                }

                issueHtml = issueHtml
                    .Replace(HtmlPlaceholders.PackageLink, packageLinkHtml);
            }
            else
            {
                issueHtml = issueHtml.Replace(HtmlPlaceholders.PackageLink, HtmlSnippets.EmptyResultSpan);
            }
            return issueHtml;
        }

        private static string CreateUserProfileLink(string userName)
        {
            var issueCreatorProfileLink =
                string.Equals("anonymous", userName, StringComparison.OrdinalIgnoreCase)
                    ? userName
                    : string.Format(HtmlSnippets.IssueCreatorTemplate, userName);

            return issueCreatorProfileLink;
        }
    }
}
