// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
namespace Gallery.CredentialExpiration.Models
{
    public class CredentialExpirationJobMetadata
    {
        public DateTimeOffset JobRunTime { get; }

        /// <summary>
        /// The cursor time. It will reflect the last job execution time.
        /// </summary>
        public DateTimeOffset CursorTime { get; }

        public int WarnDaysBeforeExpiration { get ; }

        public CredentialExpirationJobMetadata(DateTimeOffset jobRunTime, DateTimeOffset cursorTime, int warnDaysBeforeExpiration)
        {
            JobRunTime = jobRunTime;
            CursorTime = cursorTime;
            WarnDaysBeforeExpiration = warnDaysBeforeExpiration;
        }
    }
}
