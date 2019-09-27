﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Lucene.Net.Store;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using NuGet.Indexing;
using NuGet.Services.BasicSearch;
using NuGet.Services.Configuration;
using Formatting = Newtonsoft.Json.Formatting;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class StartedWebApp : IDisposable
    {
        private TestSettings _settings;
        private INupkgDownloader _nupkgDownloader;
        private LuceneDirectoryInitializer _luceneDirectoryInitializer;
        private PortReserver _portReserver;
        private IDisposable _webApp;

        private StartedWebApp()
        {
        }

        public static async Task<StartedWebApp> StartAsync(IEnumerable<PackageVersion> packages = null)
        {
            var startedWebApp = new StartedWebApp();
            await startedWebApp.InitializeAsync(packages);
            return startedWebApp;
        }

        private async Task InitializeAsync(IEnumerable<PackageVersion> packages = null)
        {
            // Establish the settings.
            _settings = ReadFromXml<TestSettings>("TestSettings.xml");
            _nupkgDownloader = new NupkgDownloader(_settings);
            _luceneDirectoryInitializer = new LuceneDirectoryInitializer(_settings, _nupkgDownloader);
            _portReserver = new PortReserver();

            // Set up the data.
            var enumeratedPackages = packages?.ToArray() ?? new PackageVersion[0];
            await _nupkgDownloader.DownloadPackagesAsync(enumeratedPackages);
            var luceneDirectory = _luceneDirectoryInitializer.GetInitializedDirectory(enumeratedPackages);

            // Set up the configuration.
            // Note that here we are using DictionaryConfigurationProvider
            // because we want to restrict the values that the configuration can hold.
            var configProvider =
                new DictionaryConfigurationProvider(
                    new Dictionary<string, string>
                    {
                        {"Local.Lucene.Directory", (luceneDirectory as FSDirectory)?.Directory.FullName ?? "RAM"},
                        {"Search.RegistrationBaseAddress", _settings.RegistrationBaseAddress}
                    });

            // Set up the data directory.
            var loader = new InMemoryLoader
            {
                { "downloads.v1.json", BuildDownloadsFile(enumeratedPackages) },
                { "curatedfeeds.json", "[]" },
                { "owners.json", "[]" },
                { "verifiedPackages.json", BuildVerifiedPackagesFile(enumeratedPackages) },
                { "rankings.v1.json", BuildRankingsFile(enumeratedPackages) },
                { "searchSettings.v1.json", JsonConvert.SerializeObject(QueryBoostingContext.Default) },
            };

            // Start the app.
            _webApp = WebApp.Start(_portReserver.BaseUri, app => new Startup().Configuration(app, new ConfigurationFactory(configProvider), luceneDirectory, loader));
            Client = new HttpClient { BaseAddress = new Uri(_portReserver.BaseUri) };
        }

        public HttpClient Client { get; private set; }

        public void Dispose()
        {
            Client?.Dispose();
            _webApp?.Dispose();
            _portReserver?.Dispose();
        }

        private static T ReadFromXml<T>(string path)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                // CodeAnalysis / XmlReader.Create: provide settings instance and set resolver property to null or instance
                var settings = new XmlReaderSettings();
                settings.XmlResolver = null;

                var reader = XmlReader.Create(stream, settings);
                return (T)xmlSerializer.Deserialize(reader);
            }
        }

        private string BuildDownloadsFile(PackageVersion[] packages)
        {
            var downloadsFile = new List<List<object>>();
            foreach (var versions in packages.GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase))
            {
                var perPackageRegistration = new List<object>();
                perPackageRegistration.Add(versions.Key);
                foreach (var version in versions)
                {
                    perPackageRegistration.Add(new List<object> { version.Version, version.Downloads });
                }

                downloadsFile.Add(perPackageRegistration);
            }

            return JsonConvert.SerializeObject(downloadsFile, Formatting.Indented);
        }

        private string BuildVerifiedPackagesFile(PackageVersion[] packages)
        {
            var verifiedPackages = packages
                .Where(p => p.Verified)
                .Select(p => p.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return JsonConvert.SerializeObject(verifiedPackages, Formatting.Indented);
        }

        private string BuildRankingsFile(PackageVersion[] packages)
        {
            var rankings = packages
                .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Sum(p => p.Downloads))
                .Select(g => g.Key)
                .ToArray();

            var rankingsFile = new { Rank = rankings };

            return JsonConvert.SerializeObject(rankingsFile, Formatting.Indented);
        }
    }
}