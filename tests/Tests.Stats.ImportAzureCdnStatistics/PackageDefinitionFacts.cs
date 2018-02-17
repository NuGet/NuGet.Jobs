// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageDefinitionFacts
    {
        [Theory]
        [InlineData("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg")]
        [InlineData("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg")]
        [InlineData("nuget.core", "1.0.1-beta1.1", "http://localhost/packages/nuget.core.1.0.1-beta1.1.nupkg")]
        [InlineData("nuget.core", "1.0.1", "http://localhost/packages/nuget.core.1.0.1.nupkg")]
        [InlineData("1", "1.0.0", "http://localhost/packages/1.1.0.0.nupkg")]
        [InlineData("dnx-mono", "1.0.0-beta7", "http://localhost/packages/dnx-mono.1.0.0-beta7.nupkg")]
        [InlineData("Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus", "6.0.1304", "http://localhost/packages/Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus.6.0.1304.nupkg")]
        [InlineData("新包", "1.0.0", "http://localhost/packages/%E6%96%B0%E5%8C%85.1.0.0.nupkg")]
        [InlineData("microsoft.applicationinsights.dependencycollector", "2.4.1", "http://localhost/packages/microsoft.applicationinsights.dependencycollector%20.2.4.1.nupkg")]
        [InlineData("xunit", "2.4.0-beta.1.build3958", "http://localhost/packages/xunit.2.4.0-beta.1.build3958.nupkg")]
        [InlineData("5.0.0.0", "5.0.0", "http://localhost/packages/5.0.0.0.5.0.0.nupkg")]
        public void ExtractsPackageIdAndVersionFromRequestUrl(string expectedPackageId, string expectedPackageVersion, string requestUrl)
        {
            var packageDefinitions = PackageDefinition.FromRequestUrl(requestUrl);
            var packageDefinition = packageDefinitions.First();
            Assert.Equal(expectedPackageId, packageDefinition.PackageId);
            Assert.Equal(expectedPackageVersion, packageDefinition.PackageVersion);
        }

        [Fact]
        public void ReturnsNullWhenInvalidPackageRequestUrl()
        {
            var packageDefinition = PackageDefinition.FromRequestUrl("http://localhost/api/v3/index.json");
            Assert.Null(packageDefinition);
        }
    }
}