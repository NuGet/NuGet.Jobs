// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gallery.CredentialExpiration;
using Gallery.CredentialExpiration.Models;

namespace Tests.CredentialExpiration
{
    public class TestCredentialExpiration : ICredentialExpirationExporter
    {
        private CredentialExpirationJobMetadata _jobMetadata;
        private DateTimeOffset _maxNotificationDate;
        private DateTimeOffset _minNotificationDate;

        private TimeSpan _skipHours;
        private GalleryCredentialExpiration _galleryCredentialsExpiration;

        public TestCredentialExpiration(CredentialExpirationJobMetadata jobMetadata, TimeSpan skipHours)
        {
            _jobMetadata = jobMetadata;
            _skipHours = skipHours;
            _galleryCredentialsExpiration = new GalleryCredentialExpiration(jobMetadata, null);
            _maxNotificationDate = _galleryCredentialsExpiration.GetMaxNotificationDate(jobMetadata);
            _minNotificationDate = _galleryCredentialsExpiration.GetMinNotificationDate(jobMetadata);
        }

        public List<ExpiredCredentialData> GetExpiredCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.GetExpiredCredentials(credentialSet);
        }

        public List<ExpiredCredentialData> GetExpiringCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.GetExpiringCredentials(credentialSet);
        }

        public async Task<List<ExpiredCredentialData>> GetCredentialsAsync(TimeSpan timeout)
        {
            List<ExpiredCredentialData> data = new List<ExpiredCredentialData>();
            DateTimeOffset indexDate = _minNotificationDate;
            int index = 0;
            while(indexDate <= _maxNotificationDate)
            {
                data.Add(new ExpiredCredentialData()
                {
                    Expires = indexDate,
                    Username = $"{index++}"
                });
                indexDate = indexDate.Add(_skipHours);
            }

            return await Task.FromResult(data);
        }
    }
}
