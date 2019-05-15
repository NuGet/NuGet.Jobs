// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Jobs.Monitoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureManagement;
using Xunit;
using Xunit.Abstractions;


namespace NuGet.Monitoring.PackageLag
{
    public class SearchInstanceRebooterFacts
    {
        private const string _region = "USSC";
        private const string _slot = "Production";
        private const string _resourceGroup = "test-rg";
        private const string _serviceName = "test-search-0";
        private const string _role = "SearchService";
        private const string _subscription = "TEST";
        private readonly ITestOutputHelper _output;
        private readonly Mock<ISearchServiceClient> _searchServiceClient;
        private readonly Mock<IAzureManagementAPIWrapper> _azureManagementAPIWrapper;
        private readonly Mock<IPackageLagTelemetryService> _telemetryService;
        private readonly Mock<IOptionsSnapshot<PackageLagMonitorConfiguration>> _configurationMock;
        private readonly Mock<IHttpClientWrapper> _httpClientMock;
        private readonly PackageLagMonitorConfiguration _configuration;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;
        private readonly CancellationToken _token;
        private readonly List<Instance> _instances;
        private readonly DateTimeOffset _feedTimestamp;
        private readonly PackageLagCatalogLeafProcessor _target;

        public SearchInstanceRebooterFacts(ITestOutputHelper output)
        {
            _output = output;
            _searchServiceClient = new Mock<ISearchServiceClient>();
            _azureManagementAPIWrapper = new Mock<IAzureManagementAPIWrapper>();
            _telemetryService = new Mock<IPackageLagTelemetryService>();
            _configurationMock = new Mock<IOptionsSnapshot<PackageLagMonitorConfiguration>>();
            _httpClientMock = new Mock<IHttpClientWrapper>();
            _configuration = new PackageLagMonitorConfiguration
            {
                InstancePortMinimum = 801,
                ServiceIndexUrl = "http://localhost:801/search/diag",
                Subscription = _subscription,
                RegionInformations = new List<RegionInformation>
                {
                    new RegionInformation
                    {
                        Region = _region,
                        ResourceGroup = _resourceGroup,
                        ServiceName = _serviceName,
                    },
                }
            };

            _logger = new LoggerFactory()
                .AddXunit(_output)
                .CreateLogger<PackageLagCatalogLeafProcessor>();

            _token = CancellationToken.None;
            _instances = new List<Instance>
            {
                new Instance(
                    _slot,
                    0,
                    "http://localhost:801/search/diag",
                    "http://localhost:801/query",
                    _region),
                new Instance(
                    _slot,
                    1,
                    "http://localhost:802/search/diag",
                    "http://localhost:802/query",
                    _region)
            };
            _feedTimestamp = new DateTimeOffset(2018, 1, 1, 8, 0, 0, TimeSpan.Zero);

            _configurationMock
                .Setup(x => x.Value)
                .Returns(() => _configuration);
            _searchServiceClient
                .Setup(x => x.GetSearchEndpointsAsync(It.IsAny<RegionInformation>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _instances);

            var regionInformations = _configuration.RegionInformations;
            var instances = new List<Instance>();

            foreach (var regionInformation in regionInformations)
            {
                instances.AddRange(_searchServiceClient.Object.GetSearchEndpointsAsync(regionInformation, _token).Result);
            }

            _target = new PackageLagCatalogLeafProcessor(instances, _httpClientMock.Object, _telemetryService.Object, _logger);
        }

        [Fact]
        public async Task ListOperationDoesNotLogCreationLag()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var oldSearchResponse = new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = currentTime,
                TotalHits = 1,
                Data = new SearchResult[1]
                {
                    new SearchResult
                    {
                        Created = currentTime - TimeSpan.FromSeconds(200),
                        LastEdited = currentTime - TimeSpan.FromSeconds(200),
                        Listed = false
                    }
                }
            };

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newSearchResponse = new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = newTime,
                TotalHits = 1,
                Data = new SearchResult[1]
                {
                    new SearchResult
                    {
                        Created = currentTime,
                        LastEdited = currentTime,
                        Listed = true
                    }
                }
            };

            var successDiagResponse = new SearchDiagnosticResponse
            {
                LastIndexReloadTime = newTime
            };


            var oldResponseMock = new Mock<IHttpResponseMessageWrapper>();
            var oldContentMock = new Mock<IHttpContentWrapper>();

            oldContentMock
                .Setup(cm => cm.ReadAsStringAsync())
                .Returns(Task.FromResult(JsonConvert.SerializeObject(oldSearchResponse)));

            oldResponseMock
                .Setup(rm => rm.Content)
                .Returns(oldContentMock.Object);

            var newResponseMock = new Mock<IHttpResponseMessageWrapper>();
            var newContentMock = new Mock<IHttpContentWrapper>();

            newContentMock
                .Setup(cm => cm.ReadAsStringAsync())
                .Returns(Task.FromResult(JsonConvert.SerializeObject(newSearchResponse)));

            newResponseMock
                .Setup(rm => rm.Content)
                .Returns(newContentMock.Object);

            var diagResponseMock = new Mock<IHttpResponseMessageWrapper>();
            var diagContentMock = new Mock<IHttpContentWrapper>();

            diagContentMock
                .Setup(cm => cm.ReadAsStringAsync())
                .Returns(Task.FromResult(JsonConvert.SerializeObject(successDiagResponse)));

            diagResponseMock
                .Setup(rm => rm.Content)
                .Returns(diagContentMock.Object);

            _httpClientMock
                .SetupSequence(x => x.GetAsync(It.Is<string>(y => y.Contains("801") && y.Contains("query")), It.IsAny<HttpCompletionOption>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldResponseMock.Object))
                .Returns(Task.FromResult(newResponseMock.Object));

            _httpClientMock
                .SetupSequence(x => x.GetAsync(It.Is<string>(y => y.Contains("802") && y.Contains("query")), It.IsAny<HttpCompletionOption>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldResponseMock.Object))
                .Returns(Task.FromResult(newResponseMock.Object));

            _httpClientMock
                .Setup(x => x.GetAsync(It.Is<string>(y => y.Contains("search/diag")), It.IsAny<HttpCompletionOption>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(diagResponseMock.Object));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            try
            {
                var success = await _target.ProcessPackageDetailsAsync(listPackageLeaf);
            }
            catch (Exception e)
            {
                Assert.Equal("Unexpected Logging", e.Message);
            }
        }
    }
}
