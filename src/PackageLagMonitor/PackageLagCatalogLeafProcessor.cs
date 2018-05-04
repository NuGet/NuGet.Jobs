using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class PackageLagCatalogLeafProcessor : ICatalogLeafProcessor
    {
        private const string SearchQueryTemplate = "?q=packageid:{0} version:{1}&ignorefilter=true&semverlevel=2.0.0";

        private const int WaitInMsBetweenPolls = 120000; // 2 minute

        private const int FailAfterCommitCount = 10;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;
        private readonly int _maxCheckedPackageCount;

        private int _currentCheckedPackageCount;
        private DateTimeOffset? _lastCommitTimestamp;
        private List<Instance> _searchInstances;
        private HttpClient _client;

        public PackageLagCatalogLeafProcessor(List<Instance> searchInstances, HttpClient client, int maxCheckedPackageCount, ILogger<PackageLagCatalogLeafProcessor> logger)
        {
            _logger = logger;
            _searchInstances = searchInstances;
            _client = client;
            _currentCheckedPackageCount = 0;
            _maxCheckedPackageCount = maxCheckedPackageCount;
        }

        public Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf)
        {
            return ProcessPackageLagDetails(leaf, /*expectListed*/ false, leaf.Published, DateTimeOffset.MinValue);
        }

        public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf)
        {
            return ProcessPackageLagDetails(leaf, leaf.IsListed(), leaf.Created, leaf.LastEdited);
        }

        private async Task<bool> ProcessPackageLagDetails(CatalogLeaf leaf, bool expectListed, DateTimeOffset created, DateTimeOffset lastEdited)
        {
            var packageId = leaf.PackageId;
            var packageVersion = leaf.PackageVersion;


            _logger.LogInformation("Computing Lag for {0} {1}", packageId, packageVersion);
            try
            {
                var cancellationToken = new CancellationToken();
                //if (_currentCheckedPackageCount < _maxCheckedPackageCount)
                //{
                var lag = await GetLagForPackageState(_searchInstances, packageId, packageVersion, expectListed, created, lastEdited, cancellationToken);
                ++_currentCheckedPackageCount;

                _logger.LogInformation($"Logging {lag.TotalSeconds} seconds lag for {packageId} {packageVersion}.");
                //}
                //else
                //{
                //    _logger.LogInformation($"Skipping package {packageId} {packageVersion} due to hitting max checked package limit");
                //}
            }
            catch
            {
                return false;
            }

            return true;

        }

        private async Task<TimeSpan> GetLagForPackageState(List<Instance> searchInstances, string packageId, string version, bool listed, DateTimeOffset created, DateTimeOffset lastEdited, CancellationToken token)
        {
            var Tasks = new List<Task<TimeSpan>>();
            foreach (Instance instance in searchInstances)
            {
                var query = instance.BaseQueryUrl + String.Format(SearchQueryTemplate, packageId, version);
                try
                {
                    _logger.LogInformation("Queueing {0}", query);
                    Tasks.Add(ComputeLagForQueries(instance.Index, query, instance.DiagUrl, listed, created, lastEdited, token));
                }
                catch (Exception e)
                {
                    _logger.LogError("An exception was encountered so no HTTP response was returned. {0}", e);
                }
            }

            var results = await Task.WhenAll(Tasks);

            var averageTicks = (long)results.Average<TimeSpan>(t => t.Ticks);

            return new TimeSpan(averageTicks);
        }

        private async Task<TimeSpan> ComputeLagForQueries(int instanceNumber, string query, string diagUrl, bool listed, DateTimeOffset created, DateTimeOffset lastEdited, CancellationToken token)
        {
            await Task.Yield();

            var resultCount = (long)0;
            var retryCount = (long)0;
            var isListOperation = false;
            TimeSpan createdDelay, v3Delay;
            DateTimeOffset lastReloadTime;
            do
            {
                using (var response = await _client.GetAsync(
                    query,
                    HttpCompletionOption.ResponseContentRead,
                    token))
                {
                    var content = response.Content;
                    var searchResultRaw = await content.ReadAsStringAsync();
                    var searchResultObject = JsonConvert.DeserializeObject<SearchResultResponse>(searchResultRaw);

                    resultCount = searchResultObject.TotalHits;

                    if (resultCount > 0)
                    {
                        if (retryCount == 0)
                        {
                            isListOperation = true;
                        }

                        resultCount = searchResultObject.Data[0].Listed == listed ? resultCount : 0;
                    }
                }

                ++retryCount;
                if (resultCount < 1)
                {
                    _logger.LogInformation("Waiting for {0} seconds before retrying", WaitInMsBetweenPolls / 1000);
                    await Task.Delay(WaitInMsBetweenPolls);
                }
            } while (resultCount < 1);


            using (var diagResponse = await _client.GetAsync(
                diagUrl,
                HttpCompletionOption.ResponseContentRead,
                token))
            {
                var diagContent = diagResponse.Content;
                var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                var searchDiagResultObject = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);

                lastReloadTime = searchDiagResultObject.LastIndexReloadTime;
            }

            createdDelay = lastReloadTime - (isListOperation ? lastEdited : created);
            v3Delay = lastReloadTime - (lastEdited == DateTimeOffset.MinValue ? created : lastEdited);

            var timeStamp = (isListOperation ? lastEdited : created);

            _logger.LogInformation("{0}:{1}: Created: {1} V3: {2}", timeStamp, query, createdDelay, v3Delay);

            return createdDelay;
        }
    }
}
