// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGet.Jobs.GitHubIndexer
{
    public static class Filters
    {
        private static readonly Regex IdRegex =
            new Regex(@"^\w+([_.-]\w+)*$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1));

        public enum ConfigFileType
        {
            MsBuild,
            PkgConfig,
            None
        }

        /// <summary>
        /// Checks whether a packageId is a valid one
        /// </summary>
        /// <param name="packageId">The packageId to verify</param>
        /// <returns>true if the packageId is of valid format, false otherwhise</returns>
        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (String.Equals(packageId, "$id$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IdRegex.IsMatch(packageId);
        }

        /// <summary>
        /// Returns the type of configuration file the filename points to. If the file type is not a valid config file,
        /// a ConfigFileType.NONE value is returned.
        /// </summary>
        /// <param name="file">The name or path of the file to look at</param>
        /// <exception cref="ArgumentNullException">Thrown then the filename is null</exception>
        /// <exception cref="ArgumentException">Thrown then the filename is invalid (Contains one or more chars defined in System.IO.Path.GetInvalidPathChars)</exception>
        /// <returns>An enum indicating the file type</returns>
        public static ConfigFileType GetConfigFileType(string file)
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.Equals(fileName, "packages.config", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigFileType.PkgConfig;
            }

            var ext = Path.GetExtension(fileName).ToLower();
            var cfgFileType = ConfigFileType.None;

            if (ext.EndsWith("proj") || ext.EndsWith(".props") || ext.EndsWith(".targets"))
            {
                cfgFileType = ConfigFileType.MsBuild;
            }

            return cfgFileType;
        }
    }
}
