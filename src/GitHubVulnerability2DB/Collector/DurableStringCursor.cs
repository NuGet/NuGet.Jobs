// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Cursor;

namespace GitHubVulnerability2Db.Collector
{
    /// <remarks>
    /// We need a specific <see cref="string"/> implementation because the existing <see cref="DurableCursor"/> only supports <see cref="System.DateTime"/>.
    /// </remarks>
    public class DurableStringCursor : ReadWriteCursor<string>
    {
        public DurableStringCursor(CloudBlockBlob cloudBlob)
        {
            _blob = cloudBlob;
        }

        private readonly CloudBlockBlob _blob;

        public override async Task Load(CancellationToken token)
        {
            string value;
            try
            {
                using (var stream = await _blob.OpenReadAsync(token))
                using (var reader = new StreamReader(stream))
                {
                    value = await reader.ReadToEndAsync();
                }
            }
            catch
            {
                value = null;
            }

            Value = value;
        }

        public override Task Save(CancellationToken token)
        {
            if (string.IsNullOrEmpty(Value))
            {
                return _blob.DeleteIfExistsAsync(token);
            }
            else
            {
                return _blob.UploadTextAsync(Value, token);
            }
        }
    }
}
