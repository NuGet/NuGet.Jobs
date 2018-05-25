// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Sql;
using Gallery.CredentialExpiration.Models;

namespace Gallery.CredentialExpiration
{
    public class GalleryCredentialExpiration : ICredentialExpiration
    {
        private ExpiredCredentialJobMetadata _jobMetadata;
        private ISqlConnectionFactory _galleryDatabase;

        public GalleryCredentialExpiration(ExpiredCredentialJobMetadata jobMetadata, ISqlConnectionFactory galleryDatabase)
        {
            _jobMetadata = jobMetadata;
            _galleryDatabase = galleryDatabase;
        }

        /// <summary>
        /// Used for the expiring credentials.
        /// </summary>
        /// <param name="jobMetadata"></param>
        /// <returns></returns>
        public DateTimeOffset GetMaxNotificationDate(ExpiredCredentialJobMetadata jobMetadata)
        {
            return jobMetadata.JobRunTime.AddDays(_jobMetadata.WarnDaysBeforeExpiration);
        }

        /// <summary>
        /// Used for the Expired credentials.
        /// </summary>
        /// <param name="jobMetadata"></param>
        /// <returns></returns>
        public DateTimeOffset GetMinNotificationDate(ExpiredCredentialJobMetadata jobMetadata)
        {
            // In case that the job failed to run for more than 1 day, go back more than the WarnDaysBeforeExpiration value 
            // with the number of days that the job did not run
            return  _jobMetadata.CursorTime;
        }

        public async Task<List<ExpiredCredentialData>> GetCredentialsAsync(TimeSpan timeout)
        {
            // Set the day interval for the accounts that will be queried for expiring /expired credentials.
            // Converts the DateTimeOffset object to UTC and outputs it using the format yyyy-MM-dd HH:mm:ssZ. (https://msdn.microsoft.com/en-us/library/bb346136(v=vs.110).aspx)
            var maxNotificationDate = GetMaxNotificationDate(_jobMetadata).ToString("yyyy-MM-dd HH:mm:ss");
            var minNotificationDate = GetMinNotificationDate(_jobMetadata).ToString("yyyy-MM-dd HH:mm:ss");

            // Connect to database
            using (var galleryConnection = await _galleryDatabase.CreateAsync())
            {
                // Fetch credentials that expire in _warnDaysBeforeExpiration days 
                // + the user's e-mail address
                return  (await galleryConnection.QueryWithRetryAsync<ExpiredCredentialData>(
                    Strings.GetExpiredCredentialsQuery,
                    param: new { MaxNotificationDate = maxNotificationDate, MinNotificationDate = minNotificationDate },
                    maxRetries: 3,
                    commandTimeout: timeout)).ToList();
            }
        }

        /// <summary>
        /// Send email of credential expired during the time interval [_jobMetadata.CursorTime, _jobMetadata.JobRunTime) 
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        public List<ExpiredCredentialData> ExpiredCredentials(List<ExpiredCredentialData> credentialSet)
        {
            // Send email to the accounts that had credentials expired from the last execution.
            return credentialSet
                          .Where(x => (x.Expires < _jobMetadata.JobRunTime))
                          .ToList();
        }

        /// <summary>
        /// Returns the expiring credentials.
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        public List<ExpiredCredentialData> ExpiringCredentials(List<ExpiredCredentialData> credentialSet)
        {
            // Send email to the accounts that will have credentials expiring in the next _warnDaysBeforeExpiration days and did not have any warning email sent yet.
            return credentialSet
                .Where(x => (x.Expires - _jobMetadata.CursorTime).TotalDays > _jobMetadata.WarnDaysBeforeExpiration)
                .ToList();
        }
    }
}
