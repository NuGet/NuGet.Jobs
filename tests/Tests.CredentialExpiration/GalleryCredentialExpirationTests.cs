// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Gallery.CredentialExpiration.Models;
using Xunit;


namespace Tests.CredentialExpiration
{
    public class GalleryCredentialExpirationTests
    {
        [Theory]
        [MemberData(nameof(Data))]
        public async void TransformRawLogLine(DateTimeOffset jobRunTime, DateTimeOffset cursorTime, int warnDaysBeforeExpiration, TimeSpan skipHours, int expectExpired, int expectExpiring)
        {
            ExpiredCredentialJobMetadata jobMetadata = new ExpiredCredentialJobMetadata(jobRunTime, cursorTime, warnDaysBeforeExpiration);
            var credExp = new TestCredentialExpiration(jobMetadata, skipHours);
            var credentialSet = await credExp.GetCredentialsAsync(default(TimeSpan));

            var expired = credExp.ExpiredCredentials(credentialSet);
            var expiring = credExp.ExpiringCredentials(credentialSet);

            Assert.Equal(expectExpired, expired.Count);
            Assert.Equal(expectExpiring, expiring.Count);
        }

        public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[]
            {
                new DateTimeOffset(year: 2018 , month: 4 , day: 6 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // jobRunTime
                new DateTimeOffset(year: 2018 , month: 4 , day: 5 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // cursorTime
                3, // warnDaysBeforeExpiration
                TimeSpan.FromHours(24), // skip number of hours when build the collection set of expirationCredentials
                1,
                1
            },
            new object[]
            {
                new DateTimeOffset(year: 2018 , month: 4 , day: 7 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // jobRunTime
                new DateTimeOffset(year: 2018 , month: 4 , day: 5 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // cursorTime
                3, // warnDaysBeforeExpiration
                TimeSpan.FromHours(24), // skip number of hours when build the collection set of expirationCredentials
                2,
                2
            },
            new object[]
            {
                new DateTimeOffset(year: 2018 , month: 4 , day: 7 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // jobRunTime
                new DateTimeOffset(year: 2018 , month: 4 , day: 6 , hour: 0 , minute: 10, second: 0, offset: TimeSpan.FromSeconds(0)), // cursorTime
                3, // warnDaysBeforeExpiration
                TimeSpan.FromHours(12), // set multiple expiration cred per day
                2,
                2
            },
        };
    }
}
