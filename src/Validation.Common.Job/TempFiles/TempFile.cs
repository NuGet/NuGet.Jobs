// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Jobs.Validation
{
    public class TempFile : ITempFile
    {
        public TempFile()
        {
            FullName = Path.GetTempFileName();
        }

        public string FullName { get; }

        public void Dispose()
        {
            try
            {
                // we'll try to delete file if it exists...
                if (File.Exists(FullName))
                {
                    File.Delete(FullName);
                }
            }
            catch
            {
                // ... but won't throw if anything goes wrong
            }
        }
    }
}
