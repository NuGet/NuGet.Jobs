// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.NotificationScheduler.Templates
{
    internal static class HtmlPlaceholders
    {
        internal const string ReportDatePlaceholder = "$$REPORT_DATE$$";
        internal const string TrendImgPlaceholder = "$$IMG_PLACEHOLDER$$";
        internal const string FromPlaceholder = "$$FROM$$";
        internal const string PackageIdPlaceholder = "$$PACKAGEID$$";
        internal const string PackageVersionPlaceholder = "$$PACKAGEVERSION$$";
        internal const string PackageLinkPlaceholder = "$$PACKAGE$$";
        internal const string ReasonPlaceholder = "$$REASON$$";
        internal const string NewIssuesPlaceholder = "$$NEWISSUES$$";
        internal const string WorkingIssuesPlaceholder = "$$WORKINGISSUES$$";
        internal const string WaitingForCustomerIssuesPlaceholder = "$$WAITINGFORCUSTOMERISSUES$$";
        internal const string PackageVersionDisplayPlaceholder = "$$PACKAGEVERSIONDISPLAY$$";
        internal const string OnCallStatusReportPlaceholder = "$$OnCallStatusReport$$";
    }
}