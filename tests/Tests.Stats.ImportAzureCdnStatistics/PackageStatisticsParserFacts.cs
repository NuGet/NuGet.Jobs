using Stats.AzureCdnLogs.Common;
using Stats.ImportAzureCdnStatistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageStatisticsParserFacts
    {
        [Theory]
        [InlineData("SemVer1Version", "1.0.0", "1.0.0")]
        [InlineData("SemVer1VersionPreRel", "1.0.0-beta", "1.0.0-beta")]
        [InlineData("SemVer2Version", "1.0.0-1.0", "1.0.0-1.0")]
        [InlineData("System.VersionEndZero", "1.0.0.0", "1.0.0")]
        [InlineData("System.VersionEndNonZero", "1.0.0.2", "1.0.0.2")]
        public void PackageVersionsAreParsedCorrectly(string packageId, string packageVersion, string expectedVersion)
        {
            // Arrange
            var logEntry = GetCdnLogEntry($"http://test.me/{packageId}.{packageVersion}.nupkg");
            var statsParser = new PackageStatisticsParser(null);

            // Act
            var stats = statsParser.FromCdnLogEntry(logEntry);

            // Assert
            Assert.Equal(packageId, stats.PackageId);
            Assert.Equal(expectedVersion, stats.PackageVersion);
        }

        private CdnLogEntry GetCdnLogEntry(string requestUrl)
        {
            return new CdnLogEntry
            {
                RequestUrl = requestUrl,
                EdgeServerTimeDelivered = DateTime.UtcNow,
                EdgeServerIpAddress = "0.0.0.0",
                UserAgent = "fakeAgent"
            };
        }
    }
}
