// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    public class TempFileFactory : ITempFileFactory
    {
        public ITempFile CreateTempFile()
            => new TempFile();

        public ITempReadOnlyFile OpenFileForReadAndDelete(string fileName)
            => new DeleteOnCloseReadOnlyTempFile(fileName);
    }
}
