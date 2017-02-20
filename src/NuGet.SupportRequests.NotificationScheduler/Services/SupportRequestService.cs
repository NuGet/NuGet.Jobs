// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.SupportRequests.NotificationScheduler.Models;

namespace NuGet.SupportRequests.NotificationScheduler.Services
{
    internal class SupportRequestService
    {
        private readonly SupportRequestRepository _supportRequestRepository;
        private readonly PagerDutyClient _pagerDutyClient;

        public SupportRequestService(
          SupportRequestRepository supportRequestRepository,
          PagerDutyConfiguration pagerDutyConfiguration)
        {
            if (supportRequestRepository == null)
            {
                throw new ArgumentNullException(nameof(supportRequestRepository));
            }

            if (pagerDutyConfiguration == null)
            {
                throw new ArgumentNullException(nameof(pagerDutyConfiguration));
            }

            _supportRequestRepository = supportRequestRepository;
            _pagerDutyClient = new PagerDutyClient(pagerDutyConfiguration.AccountName, pagerDutyConfiguration.ApiKey);
        }

        public async Task<WeeklySummaryDataModel> GetWeeklySummaryDataAsync(DateTime dateUtc)
        {
            WeeklySummaryDataModel dataModel;

            using (var connection = await _supportRequestRepository.OpenConnectionAsync())
            {
                var unresolvedIssues = await _supportRequestRepository.GetUnresolvedIssues(connection);

                dataModel = new WeeklySummaryDataModel(dateUtc, unresolvedIssues);

                dataModel.IssuesCreatedLastWeek =
                    await _supportRequestRepository.GetIssueCountCreatedLastWeek(connection, dateUtc);

                dataModel.IssuesCreatedPriorWeek =
                    await _supportRequestRepository.GetIssueCountCreatedPriorWeek(connection, dateUtc);

                dataModel.IssuesClosedLastWeek =
                    await _supportRequestRepository.GetIssueCountClosedLastWeek(connection, dateUtc);

                dataModel.IssuesClosedPriorWeek =
                    await _supportRequestRepository.GetIssueCountClosedPriorWeek(connection, dateUtc);

                dataModel.AverageTimeToResolutionLastWeek =
                    await _supportRequestRepository.GetAverageTimeToResolutionLastWeek(connection, dateUtc);

                dataModel.AverageTimeToResolutionPriorWeek =
                    await _supportRequestRepository.GetAverageTimeToResolutionPriorWeek(connection, dateUtc);

                dataModel.TopSupportRequestReasonsLastWeek =
                    await _supportRequestRepository.GetTopSupportRequestReasonsLastWeek(connection, dateUtc);
            }

            return dataModel;
        }

        public async Task<DailySummaryDataModel> GetDailySummaryDataAsync(DateTime dateUtc)
        {
            DailySummaryDataModel dataModel;

            var onCallAlias = await _pagerDutyClient.GetPrimaryOnCallAsync();
            var targetEmailAddress = $"{onCallAlias}@microsoft.com";

            using (var connection = await _supportRequestRepository.OpenConnectionAsync())
            {
                var unresolvedIssues = await _supportRequestRepository.GetUnresolvedIssues(connection, onCallAlias);

                dataModel = new DailySummaryDataModel(dateUtc, unresolvedIssues, targetEmailAddress);
            }

            return dataModel;
        }
    }
}
