// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.NotificationScheduler.Services
{
    internal static class SqlQuery
    {
        private const string _lastWeek =
            "BETWEEN DATEADD(DAY, -7, @referenceTime) AND @referenceTime";
        private const string _priorWeek =
            "BETWEEN DATEADD(DAY, -14, @referenceTime) AND DATEADD(DAY, -7, @referenceTime)";

        internal static string GetUnresolvedIssues(string onCallPagerDutyUserName = null)
        {
            var query =
                "SELECT I.[CreatedBy], I.[CreatedDate], I.[PackageId], I.[PackageVersion], I.[OwnerEmail], I.[Reason], I.[PackageRegistrationKey], A.[PagerDutyUsername] AS \'AdminPagerDutyUsername\', A.[GalleryUsername] AS \'AdminGalleryUsername\', I.[IssueStatusId] AS \'IssueStatus\' FROM[dbo].[Issues] AS I(NOLOCK) INNER JOIN[dbo].[Admins] AS A(NOLOCK) ON I.[AssignedToId] = A.[Key] WHERE I.[IssueStatusId] <> 3 ";

            if (!string.IsNullOrEmpty(onCallPagerDutyUserName))
            {
                query += "AND A.[PagerDutyUserName] = @pagerDutyUserName ";
            }

            query += "ORDER BY I.[CreatedDate] ASC";

            return query;
        }

        internal const string GetTopSupportRequestReasonsLastWeek =
            "SELECT COUNT(I.[Key]) AS \'IssueCount\', I.[Reason] FROM [dbo].[Issues] AS I (NOLOCK) WHERE I.[CreatedDate] " + _lastWeek + " GROUP BY I.[Reason] ORDER BY [IssueCount] DESC";

        private const string _getIssueCountCreated =
            "SELECT COUNT(I.[Key]) FROM [dbo].[Issues] AS I (NOLOCK) WHERE I.[CreatedDate] ";
        internal const string GetIssueCountCreatedLastWeek = _getIssueCountCreated + _lastWeek;
        internal const string GetIssueCountCreatedPriorWeek = _getIssueCountCreated + _priorWeek;

        private const string _getIssueCountClosed =
            "SELECT COUNT(I.[Key]) FROM [dbo].[Issues] AS I (NOLOCK) INNER JOIN [dbo].[History] AS H (NOLOCK) ON I.[Key] = H.[IssueId] WHERE I.[IssueStatusId] = 3 AND H.[IssueStatusId] = 3 AND H.[EntryDate] ";
        internal const string GetIssueCountClosedLastWeek = _getIssueCountClosed + _lastWeek;
        internal const string GetIssueCountClosedPriorWeek = _getIssueCountClosed + _priorWeek;

        private const string _getAverageTimeToResolution =
            "SELECT CAST(AVG(CAST(T.[ResolutionTime] AS FLOAT)) AS DATETIME) AS \'AvgResolutionTime\' FROM (SELECT MAX(H.[EntryDate]) - I.[CreatedDate] AS \'ResolutionTime\' FROM [dbo].[Issues] AS I (NOLOCK) INNER JOIN[dbo].[History] AS H (NOLOCK) ON I.[Key] = H.[IssueId]WHERE I.[IssueStatusId] = 3 AND H.[IssueStatusId] = 3 AND I.[CreatedDate] ";
        internal const string GetAverageTimeToResolutionLastWeek =
            _getAverageTimeToResolution + _lastWeek + " GROUP BY I.[Key], I.[CreatedDate]) AS T";
        internal const string GetAverageTimeToResolutionPriorWeek =
            _getAverageTimeToResolution + _priorWeek + " GROUP BY I.[Key], I.[CreatedDate]) AS T";
    }
}