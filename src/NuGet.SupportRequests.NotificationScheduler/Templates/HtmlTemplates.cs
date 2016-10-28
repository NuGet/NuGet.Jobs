﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.NotificationScheduler.Templates
{
    internal static class HtmlTemplates
    {
        internal const string IssueCreatorTemplate = "<a href=\"https://www.nuget.org/profiles/{0}\">{0}</a>";
        internal const string EmptyResultSpan = "<span style=\"color: #7e338c\">─</span>";
        internal static readonly string NoChangesSpan = $"{EmptyResultSpan} &nbsp; <a href=\"\">no changes</a>";

        internal static readonly string TrendImgTemplate =
            $"<span class=\"arr\"><img alt=\"\" src=\"{HtmlPlaceholders.TrendImgPlaceholder}\" width=\"16\"></span> &nbsp; ";

        internal static readonly string PackageLinkTemplate =
            $"<a href=\"https://www.nuget.org/packages/{HtmlPlaceholders.PackageIdPlaceholder}/{HtmlPlaceholders.PackageVersionPlaceholder}\">{HtmlPlaceholders.PackageIdPlaceholder}{HtmlPlaceholders.PackageVersionDisplayPlaceholder}</a>";

        internal static readonly string IssueTemplate =
            $"<tr class=\"border-trim\"><th class=\"left\" align=\"left\">{HtmlPlaceholders.FromPlaceholder}</th><td style=\"text-align:left;\">{HtmlPlaceholders.ReasonPlaceholder}</td><td colspan=\"2\" style=\"text-align:left;\">{HtmlPlaceholders.PackageLinkPlaceholder}</td></tr>";

        internal const string OnCallStatusTemplate =
                "<tr class=\"border-trim\"><th class=\"left\" align=\"left\" valign=\"top\" style=\"text-align: right;\">{0}</th><td valign=\"top\" style=\"text-align: left;\" colspan=\"3\">{1}</td></tr>";
        internal const string OnCallStatusItemTemplate = "<span style=\"font-weight:bold;\">{0}</span> issues <span style=\"font-weight:bold;\">{1}</span>";
        internal const string OnCallStatusItemTemplateSingle = "<span style=\"font-weight:bold;\">{0}</span> issue <span style=\"font-weight:bold;\">{1}</span>";
        internal const string OnCallStatusItemSeparator = "<br/>";
    }
}