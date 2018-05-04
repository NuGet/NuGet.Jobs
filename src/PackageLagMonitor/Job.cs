// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureManagement;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class Job : JobBase
    {
        /// <summary>
        /// To be used for <see cref="IAzureManagementAPIWrapper"/> request
        /// </summary>
        private const string ProductionSlot = "production";

        private IQueueClient _queueClient;
        private HttpClient _httpClient;
        private ICatalogClient _catalogClient;
        private int _instancePortMinimum;
        private string _serviceIndexUrl;
        private IAzureManagementAPIWrapper _azureManagementApiWrapper;
        private string _resourceGroup;
        private string _subscription;
        private string _serviceName;
        private string _region;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    if (policyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch || policyErrors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }

                    return false;
                };

            _httpClient = new HttpClient(handler);
            _catalogClient = new CatalogClient(_httpClient, LoggerFactory.CreateLogger<CatalogClient>());
            _instancePortMinimum = Int32.TryParse(jobArgsDictionary["instancePortMinimum"], out _instancePortMinimum) ? _instancePortMinimum : 44301;
            _serviceIndexUrl = jobArgsDictionary["serviceIndexUrl"];
            _subscription = jobArgsDictionary["subscription"];
            _serviceName = jobArgsDictionary["serviceName"];
            _resourceGroup = jobArgsDictionary["resourceGroup"];
            _region = jobArgsDictionary["region"];

            var serviceBusConnectionString = jobArgsDictionary["serviceBusConnectionString"];
            var queueName = jobArgsDictionary["queueName"];
            _queueClient = new QueueClient(serviceBusConnectionString, queueName);

            var azureManagementAPIWrapperConfiguration = new AzureManagementAPIWrapperConfiguration(jobArgsDictionary["azureClientId"], jobArgsDictionary["azureClientSecret"]);
            _azureManagementApiWrapper = new AzureManagementAPIWrapper(azureManagementAPIWrapperConfiguration);
        }

        public async override Task Run()
        {
            var token = new CancellationToken();
            try
            {
                var instances = await GetSearchEndpointsAsync(token);

                var maxCommit = DateTimeOffset.MinValue;

                foreach (Instance instance in instances)
                {
                    try
                    {
                        using (var diagResponse = await _httpClient.GetAsync(
                            instance.DiagUrl,
                            HttpCompletionOption.ResponseContentRead,
                            token))
                        {
                            var diagContent = diagResponse.Content;
                            var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                            var searchDiagResultObject = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);

                            var commitDateTime = DateTimeOffset.Parse(searchDiagResultObject.CommitUserData.CommitTimeStamp);

                            maxCommit = commitDateTime > maxCommit ? commitDateTime : maxCommit;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("An exception was encountered so no HTTP response was returned. {0}", e);
                    }
                }
                
                var catalogLeafProcessor = new PackageLagCatalogLeafProcessor(instances, _httpClient, _queueClient, _region, _subscription, LoggerFactory.CreateLogger<PackageLagCatalogLeafProcessor>());

                var settings = new CatalogProcessorSettings
                {
                    ServiceIndexUrl = _serviceIndexUrl,
                    DefaultMinCommitTimestamp = maxCommit,
                    ExcludeRedundantLeaves = false
                };

                var start = new FileCursor("cursor.json", LoggerFactory.CreateLogger<FileCursor>());
                await start.SetAsync(maxCommit.AddTicks(1).AddHours(-7));

                var catalogProcessor = new CatalogProcessor(start, _catalogClient, catalogLeafProcessor, settings, LoggerFactory.CreateLogger<CatalogProcessor>());

                bool success;
                do
                {
                    success = await catalogProcessor.ProcessAsync();
                    if (!success || !await catalogLeafProcessor.WaitForProcessing())
                    {
                        Console.WriteLine("Processing the catalog leafs failed. Retrying.");
                    }
                }
                while (!success);

                return;
            }
            catch (Exception e)
            {
                var output = new List<string>();
                output.Add(e.Message);
                output.Add(e.StackTrace);

                Logger.LogError("Exception Occured. {0}", e);

                return;
            }
        }

        private async Task<List<Instance>> GetSearchEndpointsAsync(CancellationToken token)
        {
            string result = await _azureManagementApiWrapper.GetCloudServicePropertiesAsync(
                                    _subscription,
                                    _resourceGroup,
                                    _serviceName,
                                    ProductionSlot,
                                    token);

            var cloudService = AzureHelper.ParseCloudServiceProperties(result);

            return GetInstances(cloudService.Uri, cloudService.InstanceCount);
        }

        private List<Instance> GetInstances(Uri endpointUri, int instanceCount)
        {
            var instancePortMinimum = _instancePortMinimum;

            Logger.LogInformation("Testing {InstanceCount} instances, starting at port {InstancePortMinimum}.", instanceCount, instancePortMinimum);

            return Enumerable
                .Range(0, instanceCount)
                .Select(i =>
                {
                    var diagUriBuilder = new UriBuilder(endpointUri);

                    diagUriBuilder.Scheme = "https";
                    diagUriBuilder.Port = instancePortMinimum + i;
                    diagUriBuilder.Path = "search/diag";

                    var queryBaseUriBuilder = new UriBuilder(endpointUri);

                    queryBaseUriBuilder.Scheme = "https";
                    queryBaseUriBuilder.Port = instancePortMinimum + i;
                    queryBaseUriBuilder.Path = "search/query";

                    return new Instance
                    {
                        Index = i,
                        DiagUrl = diagUriBuilder.Uri.ToString(),
                        BaseQueryUrl = queryBaseUriBuilder.Uri.ToString()
                    };
                })
                .ToList();
        }
    }
}
