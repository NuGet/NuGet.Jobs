// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ConfigFileParser : IConfigFileParser
    {
        private readonly RepoUtils _repoUtils;
        private readonly ILogger<ConfigFileParser> _logger;

        public ConfigFileParser(RepoUtils repoUtils, ILogger<ConfigFileParser> logger)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<string> Parse(ICheckedOutFile file)
        {
            _logger.LogTrace("[{RepoName}] Parsing file {FileName} !", file.RepoId, file.Path);

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

                _logger.LogDebug("[{RepoName}] Found {Count} dependencies!", file.RepoId, res.Count);

                return res;
            }
        }
    }
}
