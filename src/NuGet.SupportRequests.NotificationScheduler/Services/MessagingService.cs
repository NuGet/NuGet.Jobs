﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.SupportRequests.NotificationScheduler.Models;
using NuGet.SupportRequests.NotificationScheduler.Templates;

namespace NuGet.SupportRequests.NotificationScheduler.Services
{
    internal class MessagingService
    {
        private readonly MailAddress _fromAddress;
        private readonly MailAddress _toAddress;
        private readonly ILogger<MessagingService> _logger;
        private readonly string _smtpUri;
        private readonly IDictionary<string, string> _templateCache;
        private SmtpClient _smtpClient;

        private const string _templatesNamespace = "NuGet.SupportRequests.NotificationScheduler.Templates";
        private static readonly string _cssStylesResourceName = $"{_templatesNamespace}.EmailStyles.css";

        private const string _noreplyAddress = "NuGet Gallery <noreply@nuget.org>";
        private const string _positiveInfinityLabel = "+ ∞";
        private const string _negativeInfinityLabel = "- ∞";

        public MessagingService(ILoggerFactory loggerFactory, string smtpUri, string targetEmail)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (smtpUri == null)
            {
                throw new ArgumentNullException(nameof(smtpUri));
            }

            if (targetEmail == null)
            {
                throw new ArgumentNullException(nameof(targetEmail));
            }

            _logger = loggerFactory.CreateLogger<MessagingService>();
            _fromAddress = new MailAddress(_noreplyAddress);
            _toAddress = new MailAddress(targetEmail);
            _smtpUri = smtpUri;
            _templateCache = new Dictionary<string, string>();
        }

        internal void SendWeeklySummaryNotification(WeeklySummaryDataModel dataModel)
        {
            var template = GetTemplate("WeeklySummary.html");
            var body = ApplyDataModelToTemplate(template, dataModel);

            SendNotification(
                "NuGet Support - Weekly Summary",
                body,
                dataModel.ReferenceTime);
        }

        internal void SendDailySummaryNotification(DailySummaryDataModel dataModel)
        {
            var template = GetTemplate("OnCallSummary.html");
            var body = ApplyDataModelToTemplate(template, dataModel);

            SendNotification(
                "NuGet Support - On-Call Daily Summary",
                body,
                dataModel.ReferenceTime,
                dataModel.OnCallEmailAddress);
        }

        private void SendNotification(
            string subject,
            string body,
            DateTime referenceTime,
            string toAddress = null)
        {
            var targetAddress = _toAddress;
            if (toAddress != null)
            {
                targetAddress = new MailAddress(toAddress);
            }

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.From = _fromAddress;
                mailMessage.ReplyToList.Add(_fromAddress);
                mailMessage.Body = body;
                mailMessage.To.Add(targetAddress);

                SendMessage(mailMessage);
            }

            _logger.LogInformation(
                "Successfully sent notification '{NotificationType}' for reference time '{ReferenceTimeUtc}'",
                subject,
                referenceTime.ToShortDateString());
        }

        private static string GetTrendImage(double percentage, string upTrendImage, string downTrendImage)
        {
            if (double.IsPositiveInfinity(percentage))
            {
                return HtmlTemplates.TrendImgTemplate.Replace(HtmlPlaceholders.TrendImage, upTrendImage);
            }
            else if (double.IsNegativeInfinity(percentage))
            {
                return HtmlTemplates.TrendImgTemplate.Replace(HtmlPlaceholders.TrendImage, downTrendImage);
            }
            else if (double.IsNaN(percentage))
            {
                return string.Empty;
            }
            else if (percentage > 0)
            {
                return HtmlTemplates.TrendImgTemplate.Replace(HtmlPlaceholders.TrendImage, upTrendImage);
            }
            else if (percentage < 0)
            {
                return HtmlTemplates.TrendImgTemplate.Replace(HtmlPlaceholders.TrendImage, downTrendImage);
            }

            return string.Empty;
        }

        private static string GetTrendPercentageString(double percentage)
        {
            if (double.IsPositiveInfinity(percentage))
            {
                return _positiveInfinityLabel;
            }
            else if (double.IsNegativeInfinity(percentage))
            {
                return _negativeInfinityLabel;
            }
            else if (!double.IsNaN(percentage) && (percentage > 0 || percentage < 0))
            {
                return percentage.ToString("P0");
            }

            return HtmlTemplates.NoChangesSpan;
        }

        private static string ApplyDataModelToTemplate(string template, WeeklySummaryDataModel dataModel)
        {
            var newIssuesTrendPct = dataModel.GetNewIssuesTrendPct();
            var newIssuesTrendImg = GetTrendImage(newIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var closedIssuesTrendPct = dataModel.GetIssuesClosedTrendPct();
            var closedIssuesTrendImg = GetTrendImage(closedIssuesTrendPct, Images.UpArrowGreen, Images.DownArrowRed);

            var unresolvedIssuesTrendPct = dataModel.GetIssuesUnresolvedTrendPct();
            var unresolvedIssuesTrendImg = GetTrendImage(unresolvedIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var waitingIssuesTrendPct = dataModel.GetIssuesWaitingForCustomerTrendPct();
            var waitingIssuesTrendImg = GetTrendImage(waitingIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var workingIssuesTrendPct = dataModel.GetIssuesInProgressTrendPct();
            var workingIssuesTrendImg = GetTrendImage(workingIssuesTrendPct, Images.UpArrowRed, Images.DownArrowGreen);

            var slaUnresolvedPctDeltaImg = GetTrendImage(dataModel.UnresolvedPercentageDelta, Images.UpArrowRed, Images.DownArrowGreen);

            var slaAverageTimeToResolutionDelta = dataModel.AverageTimeToResolutionLastWeek - dataModel.AverageTimeToResolutionPriorWeek;
            var slaAverageTimeToResolutionImg = GetTrendImage(slaAverageTimeToResolutionDelta.Ticks, Images.UpArrowRed, Images.DownArrowGreen);

            var result = template
                .Replace(HtmlPlaceholders.ReportDate, dataModel.ReferenceTime.ToString("dd/MM/yy"))

                // new support requests
                .Replace(HtmlPlaceholders.NewIssueCountPriorWeek, dataModel.IssuesCreatedPriorWeek.ToString("N0"))
                .Replace(HtmlPlaceholders.NewIssueCountLastWeek, dataModel.IssuesCreatedLastWeek.ToString("N0"))
                .Replace(HtmlPlaceholders.NewIssueCountDeltaValue, GetTrendPercentageString(newIssuesTrendPct))
                .Replace(HtmlPlaceholders.NewIssueCountDeltaImage, newIssuesTrendImg)

                // closed support requests
                .Replace(HtmlPlaceholders.ClosedIssueCountPriorWeek, dataModel.IssuesClosedPriorWeek.ToString("N0"))
                .Replace(HtmlPlaceholders.ClosedIssueCountLastWeek, dataModel.IssuesClosedLastWeek.ToString("N0"))
                .Replace(HtmlPlaceholders.ClosedIssueCountDeltaValue, GetTrendPercentageString(closedIssuesTrendPct))
                .Replace(HtmlPlaceholders.ClosedIssueCountDeltaImage, closedIssuesTrendImg)

                // unresolved support requests
                .Replace(HtmlPlaceholders.UnresolvedIssueCountPriorWeek, dataModel.PriorWeek.UnresolvedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountLastWeek, dataModel.LastWeek.UnresolvedCount.ToString("N0"))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountDeltaValue, GetTrendPercentageString(unresolvedIssuesTrendPct))
                .Replace(HtmlPlaceholders.UnresolvedIssueCountDeltaImage, unresolvedIssuesTrendImg)

                // waiting on customer
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountPriorWeek, dataModel.PriorWeek.WaitingForCustomerCount.ToString("N0"))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountLastWeek, dataModel.LastWeek.WaitingForCustomerCount.ToString("N0"))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountDeltaValue, GetTrendPercentageString(waitingIssuesTrendPct))
                .Replace(HtmlPlaceholders.WaitingForCustomerIssueCountDeltaImage, waitingIssuesTrendImg)

                // in progress
                .Replace(HtmlPlaceholders.InProgressIssueCountPriorWeek, dataModel.PriorWeek.InProgressCount.ToString("N0"))
                .Replace(HtmlPlaceholders.InProgressIssueCountLastWeek, dataModel.LastWeek.InProgressCount.ToString("N0"))
                .Replace(HtmlPlaceholders.InProgressIssueCountDeltaValue, GetTrendPercentageString(workingIssuesTrendPct))
                .Replace(HtmlPlaceholders.InProgressIssueCountDeltaImage, workingIssuesTrendImg)

                // SLA - unresolved pct
                .Replace(HtmlPlaceholders.UnresolvedPercentagePriorWeek, dataModel.GetUnresolvedPercentagePriorWeek().ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageLastWeek, dataModel.GetUnresolvedPercentageLastWeek().ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageDeltaValue, dataModel.UnresolvedPercentageDelta.ToString("P0"))
                .Replace(HtmlPlaceholders.UnresolvedPercentageDeltaImage, slaUnresolvedPctDeltaImg)

                // SLA - average time to resolution
                .Replace(HtmlPlaceholders.AvgTimeToResolutionPriorWeek, dataModel.AverageTimeToResolutionPriorWeek.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionLastWeek, dataModel.AverageTimeToResolutionLastWeek.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionDeltaValue, slaAverageTimeToResolutionDelta.ToString(@"d\d' 'hh\:mm"))
                .Replace(HtmlPlaceholders.AvgTimeToResolutionDeltaImage, slaAverageTimeToResolutionImg);

            // Top 3 support request reasons last week
            result = FillInTopSupportRequestReasons(dataModel.TopSupportRequestReasonsLastWeek, result);

            // who has open issues assigned
            result = FillInUnResolvedIssuesByAssignedTo(dataModel, result);

            return result;
        }

        private static string ApplyDataModelToTemplate(string template, DailySummaryDataModel dataModel)
        {
            var result = template;
            var referenceTimeDisplay = dataModel.ReferenceTime.ToString("dd/MM/yy");

            if (dataModel.UnresolvedIssues.Any())
            {
                result = InjectIssueStatus(
                    dataModel.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.New),
                    result, HtmlPlaceholders.NewIssues);

                result = InjectIssueStatus(
                    dataModel.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.Working),
                    result, HtmlPlaceholders.WorkingIssues);

                result = InjectIssueStatus(
                    dataModel.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.WaitingForCustomer),
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

            var issueHtmlTemplate = HtmlTemplates.IssueTemplate
                .Replace(HtmlPlaceholders.From,
                    $"[{supportRequest.CreatedDate:MM/dd hh:mm tt} UTC] - {issueCreatorProfileLink}");

            var issueHtml = issueHtmlTemplate.Replace(HtmlPlaceholders.Reason, supportRequest.Reason);

            if (!string.IsNullOrEmpty(supportRequest.PackageId))
            {
                var packageLinkHtml = HtmlTemplates.PackageLinkTemplate
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
                issueHtml = issueHtml.Replace(HtmlPlaceholders.PackageLink, HtmlTemplates.EmptyResultSpan);
            }
            return issueHtml;
        }

        private static string CreateUserProfileLink(string userName)
        {
            var issueCreatorProfileLink =
                string.Equals("anonymous", userName, StringComparison.OrdinalIgnoreCase)
                    ? userName
                    : string.Format(HtmlTemplates.IssueCreatorTemplate, userName);

            return issueCreatorProfileLink;
        }

        private static string FillInUnResolvedIssuesByAssignedTo(
            WeeklySummaryDataModel dataModel,
            string result)
        {
            var oncallStatusReportBuilder = new StringBuilder();

            foreach (var unresolvedIssueGroup in dataModel.UnresolvedIssues.GroupBy(i => i.AdminGalleryUsername).OrderByDescending(i => i.Count()))
            {
                var status = string.Empty;
                foreach (var unresolvedIssueStatusGroup in unresolvedIssueGroup.GroupBy(i => i.IssueStatus).OrderByDescending(i => i.Count()))
                {
                    if (!string.IsNullOrEmpty(status))
                    {
                        status += HtmlTemplates.OnCallStatusItemSeparator;
                    }

                    var issueCount = unresolvedIssueStatusGroup.Count();

                    if (issueCount > 1)
                    {
                        status += string.Format(
                            HtmlTemplates.OnCallStatusItemTemplate,
                            unresolvedIssueStatusGroup.Count(),
                            Enum.GetName(typeof(IssueStatusKeys), unresolvedIssueStatusGroup.Key));
                    }
                    else
                    {
                        status += string.Format(
                            HtmlTemplates.OnCallStatusItemTemplateSingle,
                            unresolvedIssueStatusGroup.Count(),
                            Enum.GetName(typeof(IssueStatusKeys), unresolvedIssueStatusGroup.Key));
                    }
                }

                oncallStatusReportBuilder.AppendFormat(HtmlTemplates.OnCallStatusTemplate, unresolvedIssueGroup.Key, status.TrimStart(','));
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
                            .Replace(position3Placeholder, HtmlTemplates.EmptyResultSpan)
                            .Replace(position3CountPlaceholder, string.Empty);
                    }
                }
                else
                {
                    result = result
                        .Replace(position2Placeholder, HtmlTemplates.EmptyResultSpan)
                        .Replace(position2CountPlaceholder, string.Empty)
                        .Replace(position3Placeholder, HtmlTemplates.EmptyResultSpan)
                        .Replace(position3CountPlaceholder, string.Empty);
                }
            }
            else
            {
                result = result
                    .Replace(position1Placeholder, HtmlTemplates.EmptyResultSpan)
                    .Replace(position1CountPlaceholder, string.Empty)
                    .Replace(position2Placeholder, HtmlTemplates.EmptyResultSpan)
                    .Replace(position2CountPlaceholder, string.Empty)
                    .Replace(position3Placeholder, HtmlTemplates.EmptyResultSpan)
                    .Replace(position3CountPlaceholder, string.Empty);
            }

            return result;
        }

        private void SendMessage(MailMessage mailMessage)
        {
            var smtpClient = GetOrCreateSmtpClient();

            var alternateHtmlView = AlternateView.CreateAlternateViewFromString(mailMessage.Body, null, "text/html");
            mailMessage.AlternateViews.Add(alternateHtmlView);

            smtpClient.Send(mailMessage);
        }

        private SmtpClient GetOrCreateSmtpClient()
        {
            if (_smtpClient != null)
            {
                return _smtpClient;
            }

            var smtpUri = new SmtpUri(new Uri(_smtpUri));
            _smtpClient = new SmtpClient();
            _smtpClient.Host = smtpUri.Host;
            _smtpClient.Port = smtpUri.Port;
            _smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            _smtpClient.EnableSsl = smtpUri.Secure;

            if (!string.IsNullOrEmpty(smtpUri.UserName))
            {
                _smtpClient.UseDefaultCredentials = false;
                _smtpClient.Credentials = new NetworkCredential(
                  smtpUri.UserName,
                  smtpUri.Password);
            }

            return _smtpClient;
        }

        private string GetTemplate(string name)
        {
            string template;
            if (_templateCache.ContainsKey(name))
            {
                template = _templateCache[name];
            }
            else
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{_templatesNamespace}.{name}";

                string htmlTemplate;
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    htmlTemplate = reader.ReadToEnd();
                }

                // apply CSS styles
                string cssStyles;
                using (var stream = assembly.GetManifestResourceStream(_cssStylesResourceName))
                using (var reader = new StreamReader(stream))
                {
                    cssStyles = reader.ReadToEnd();
                }

                template = htmlTemplate
                    .Replace(HtmlPlaceholders.CssStyles, cssStyles);

                _templateCache[name] = template;
            }

            return template;
        }
    }
}
