﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class CdnLogEntryParserFacts
    {
        public class TheParseLogEntryFromLineMethod
        {
            private const int LineNumber = 42;
            private const string StatusLineFormat = "1507030253 0 - - 0.1.2.3 443 {0} 577 GET http://example/path - 0 653  \"UserAgent\" 56086 \"NuGet-Operation: - NuGet-DependentPackage: - NuGet-ProjectGuids: -\"  ";
            
            [Theory]
            [InlineData("TCP_MISS/0")]
            [InlineData("TCP_MISS/199")]
            [InlineData("TCP_MISS/300")]
            [InlineData("TCP_MISS/404")]
            [InlineData("SOMETHING_ELSE/404")]
            [InlineData("TCP_MISS/504")]
            [InlineData("TCP_MISS/604")]
            [InlineData("0")]
            [InlineData("304")]
            [InlineData("400")]
            [InlineData("404")]
            [InlineData("500")]
            [InlineData("TCP_UNKNOWN/0")]
            [InlineData("TCP_UNKNOWN/304")]
            [InlineData("TCP_UNKNOWN/400")]
            [InlineData("TCP_UNKNOWN/404")]
            [InlineData("TCP_UNKNOWN/500")]
            public void IgnoresNon200HttpStatusCodes(string status)
            {
                // Arrange
                var line = string.Format(StatusLineFormat, status);

                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    line,
                    FailOnError);

                // Assert
                Assert.Null(logEntry);
            }

            [Theory]
            [InlineData("TCP_MISS/200")]
            [InlineData("TCP_MISS/299")]
            [InlineData("TCP_MISS/")]
            [InlineData("TCP_MISS")]
            [InlineData("200")]
            public void DoesNotIgnore200LevelAndUnrecognizedHttpStatusCodes(string status)
            {
                // Arrange
                var line = string.Format(StatusLineFormat, status);

                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    line,
                    (e, lineNumber) => Assert.False(true, "The error action should not be called."));

                // Assert
                Assert.NotNull(logEntry);
                Assert.Equal(status, logEntry.CacheStatusCode);
            }

            private static void FailOnError(Exception e, int lineNumber)
            {
                Assert.False(true, "The error action should not be called.");
            }
        }
    }
}