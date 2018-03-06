﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageTranslator
    {
        private readonly Dictionary<string, List<PackageTranslation>> _packageTranslations
            = new Dictionary<string, List<PackageTranslation>>(StringComparer.OrdinalIgnoreCase);

        public PackageTranslator(string packageTranslationsJsonPath)
        {
            if (!File.Exists(packageTranslationsJsonPath))
            {
                throw new FileNotFoundException("Could not find file.", packageTranslationsJsonPath);
            }

            var packageTranslationsJson = JObject.Parse(
                File.ReadAllText(packageTranslationsJsonPath));

            var packageTranslationsArray = (JArray) packageTranslationsJson["translations"];
            foreach (var packageTranslationJson in packageTranslationsArray.Children<JObject>())
            {
                var packageTranslation = new PackageTranslation
                {
                    IncorrectPackageId = packageTranslationJson["incorrectpackageid"].ToString(),
                    IncorrectPackageVersionPattern = new Regex(string.Format("^{0}$", packageTranslationJson["incorrectpackageversionpattern"])),
                    CorrectedPackageId = packageTranslationJson["correctedpackageid"].ToString(),
                    CorrectedPackageVersionPattern = packageTranslationJson["correctedpackageversionpattern"].ToString()
                };

                if (!_packageTranslations.ContainsKey(packageTranslation.IncorrectPackageId))
                {
                    _packageTranslations.Add(packageTranslation.IncorrectPackageId, new List<PackageTranslation>());
                }

                _packageTranslations[packageTranslation.IncorrectPackageId].Add(packageTranslation);
            }
        }

        public bool TryTranslatePackageDefinition(PackageDefinition packageDefinition)
        {
            bool translateOccurred = false;

            if (packageDefinition != null
                && !string.IsNullOrEmpty(packageDefinition.PackageId)
                && !string.IsNullOrEmpty(packageDefinition.PackageVersion)
                && _packageTranslations.ContainsKey(packageDefinition.PackageId))
            {
                var potentialTranslations = _packageTranslations[packageDefinition.PackageId];
                foreach (var potentialTranslation in potentialTranslations)
                {
                    var correctedPackageVersion = potentialTranslation.IncorrectPackageVersionPattern
                        .Replace(packageDefinition.PackageVersion,
                            potentialTranslation.CorrectedPackageVersionPattern);

                    if (!packageDefinition.PackageVersion.Equals(correctedPackageVersion))
                    {
                        packageDefinition.PackageId = potentialTranslation.CorrectedPackageId;
                        packageDefinition.PackageVersion = correctedPackageVersion;
                        translateOccurred = true;
                        break;
                    }
                }
            }

            return translateOccurred;
        }
    }
}