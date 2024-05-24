// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    internal static class SimpleCloudBlobExtensions
    {
        public static async Task<Stream> OpenReadAsync(this ISimpleCloudBlob blob, IAccessCondition accessCondition)
        {
            return await blob.OpenReadAsync(accessCondition);
        }

        public static async Task<Stream> OpenWriteAsync(this ISimpleCloudBlob blob, IAccessCondition accessCondition)
        {
            return await blob.OpenWriteAsync(accessCondition);
        }
    }
}
