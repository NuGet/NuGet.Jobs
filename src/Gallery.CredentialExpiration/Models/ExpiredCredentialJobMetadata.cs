// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
namespace Gallery.CredentialExpiration.Models
{
    public class ExpiredCredentialJobMetadata
    {
        public DateTimeOffset JobRunTime { get; }

        public DateTimeOffset CursorTime { get; }

        public int WarnDaysBeforeExpiration { get ; }

        public ExpiredCredentialJobMetadata(DateTimeOffset jobRunTime, DateTimeOffset cursorTime, int warnDaysBeforeExpiration)
        {
            JobRunTime = jobRunTime;
            CursorTime = cursorTime;
            WarnDaysBeforeExpiration = warnDaysBeforeExpiration;
        }
    }
}
