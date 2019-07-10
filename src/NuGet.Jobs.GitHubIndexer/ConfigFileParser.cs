// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ConfigFileParser : IConfigFileParser
    {
        private readonly RepoUtils _repoUtils;

        public ConfigFileParser(RepoUtils repoUtils)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
        }

        public IReadOnlyList<string> Parse(ICheckedOutFile file)
        {
            using (var fileStream = file.openFile())
            {
                IReadOnlyList<string> res;
                if (Filters.GetConfigFileType(file.Path) == Filters.ConfigFileType.PkgConfig)
                {
                    res = _repoUtils.ParsePackagesConfig(fileStream, file.RepoId);
                }
                else
                {
                    res = _repoUtils.ParseProjFile(fileStream, file.RepoId);
                }

                //_logger.LogDebug("[{RepoName}] Found {Count} dependencies!", file.RepoId, res.Count);

                return res;
            }
        }
    }
}
