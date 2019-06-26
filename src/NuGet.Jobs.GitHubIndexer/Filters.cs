// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGet.Jobs.GitHubIndexer
{
    public static class Filters
    {
        private static readonly Regex IdRegex = new Regex(@"^\w+([_.-]\w+)*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public enum ConfigFileType
        {
            PROJ,
            PROPS,
            TARGETS,
            PKG_CONFIG,
            NONE
        }

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
        /// <param name="fileName">The name of the file to look at</param>
        /// <exception cref="ArgumentNullException">Thrown then the filename is null</exception>
        /// <exception cref="ArgumentException">Thrown then the filename is invalid</exception>
        /// <returns>An enum indicating the file type</returns>
        public static ConfigFileType GetConfigFileType(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.Equals(fileName, "packages.config", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigFileType.PKG_CONFIG;
            }

            var ext = Path.GetExtension(fileName).ToLower();
            var cfgFileType = ConfigFileType.NONE;

            if (ext.EndsWith("proj"))
            {
                cfgFileType = ConfigFileType.PROJ;
            }
            else if (ext.EndsWith(".props"))
            {
                cfgFileType = ConfigFileType.PROPS;
            }
            else if (ext.EndsWith(".targets"))
            {
                cfgFileType = ConfigFileType.TARGETS;
            }

            return cfgFileType;
        }
    }
}
