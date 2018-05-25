// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gallery.CredentialExpiration;
using Gallery.CredentialExpiration.Models;

namespace Tests.CredentialExpiration
{
    public class TestCredentialExpiration : ICredentialExpiration
    {
        private ExpiredCredentialJobMetadata _jobMetadata;
        private DateTimeOffset _maxNotificationDate;
        private DateTimeOffset _minNotificationDate;

        private TimeSpan _skipHours;
        private GalleryCredentialExpiration _galleryCredentialsExpiration;

        public TestCredentialExpiration(ExpiredCredentialJobMetadata jobMetadata, TimeSpan skipHours)
        {
            _jobMetadata = jobMetadata;
            _skipHours = skipHours;
            _galleryCredentialsExpiration = new GalleryCredentialExpiration(jobMetadata, null);
            _maxNotificationDate = _galleryCredentialsExpiration.GetMaxNotificationDate(jobMetadata);
            _minNotificationDate = _galleryCredentialsExpiration.GetMinNotificationDate(jobMetadata);
        }

        public List<ExpiredCredentialData> ExpiredCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.ExpiredCredentials(credentialSet);
        }

        public List<ExpiredCredentialData> ExpiringCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.ExpiringCredentials(credentialSet);
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
