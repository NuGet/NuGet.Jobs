﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;

namespace NuGet.Jobs.Validation
{
    public class TempFileFactory : ITempFileFactory
    {
        public ITempFile CreateTempFile(string contents)
        {
            var file = new TempFile();

            File.WriteAllText(file.FullName, contents, Encoding.UTF8);

            return file;
        }

        public ITempReadOnlyFile OpenFileForReadAndDelete(string fileName)
            => new DeleteOnCloseReadOnlyTempFile(fileName);
    }
}
