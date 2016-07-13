// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Jobs.Validation.Common
{
    public static class NuGetPackageQueueExtensions
    {
        private const int TruncatedPropertyMaxLength = 4000;

        /// <summary>
        /// Azure Queues have a max message length of 65536 bytes.
        /// This method truncates potentially long fields so that a serialized representation
        /// of the package falls within that boundary.
        /// </summary>
        /// <param name="package">The package to truncate</param>
        /// <returns>Truncated package</returns>
        public static NuGetPackage TruncateForAzureQueue(this NuGetPackage package)
        {
            // Clone the package
            var clone = JsonConvert.DeserializeObject<NuGetPackage>(
                JsonConvert.SerializeObject(package));

            // Truncate long properties
            clone.Description = Truncate(clone.Description, TruncatedPropertyMaxLength);
            clone.ReleaseNotes = Truncate(clone.ReleaseNotes, TruncatedPropertyMaxLength);
            clone.Summary = Truncate(clone.Summary, TruncatedPropertyMaxLength);
            clone.Tags = Truncate(clone.Tags, TruncatedPropertyMaxLength);

            return clone;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value.Substring(0, maxLength - 1);
            }

            return value;
        }
    }
}