// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Validation.Common.Job.Tests
{
    public static class PathUtility
    {
        public static bool IsFilePathAbsolute(string path)
            => Path.IsPathRooted(path)
                    && Path.GetPathRoot(path).Length == 3
                    && Path.GetPathRoot(path).Substring(1) == ":" + Path.DirectorySeparatorChar;
    }
}
