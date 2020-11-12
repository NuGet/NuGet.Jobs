﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Autofac;
using Microsoft.Azure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Microsoft.Rest.TransientFaultHandling;
using Microsoft.WindowsAzure.Storage;
using NuGet.Protocol;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Auxiliary2AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public static class DependencyInjectionExtensions
    {
        public static ContainerBuilder AddAzureSearch(this ContainerBuilder containerBuilder)
        {
            containerBuilder.AddFeatureFlags();

            /// Here, we register services that depend on an interface that there are multiple implementations.

            /// There are multiple implementations of <see cref="ISearchServiceClientWrapper"/>.
            RegisterIndexServices(containerBuilder, "SearchIndex", "HijackIndex");

            /// There are multiple implementations of storage, in particular <see cref="ICloudBlobClient"/>.
            RegisterAzureSearchStorageServices(containerBuilder, "AzureSearchStorage");
            RegisterAuxiliaryDataStorageServices(containerBuilder, "AuxiliaryDataStorage");

            return containerBuilder;
        }

        private static void RegisterIndexServices(ContainerBuilder containerBuilder, string searchIndexKey, string hijackIndexKey)
        {
            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.SearchIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(searchIndexKey);

            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.HijackIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(hijackIndexKey);

            containerBuilder
                .Register<IBatchPusher>(c => new BatchPusher(
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<IVersionListDataClient>(),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>(),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<BatchPusher>>()));

            containerBuilder
                .Register<ISearchService>(c => new AzureSearchService(
                    c.Resolve<IIndexOperationBuilder>(),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<ISearchResponseBuilder>(),
                    c.Resolve<IAzureSearchTelemetryService>()));

            containerBuilder
                .Register<ISearchStatusService>(c => new SearchStatusService(
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<ISearchParametersBuilder>(),
                    c.Resolve<IAuxiliaryDataCache>(),
                    c.Resolve<ISecretRefresher>(),
                    c.Resolve<IOptionsSnapshot<SearchServiceConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<SearchStatusService>>()));
        }

        private static void RegisterAzureSearchStorageServices(ContainerBuilder containerBuilder, string key)
        {
            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.StorageConnectionString,
                        DefaultBlobRequestOptions.Create());
                })
                .Keyed<ICloudBlobClient>(key);

            containerBuilder
                .Register<IVersionListDataClient>(c => new VersionListDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<VersionListDataClient>>()));

            containerBuilder
                .Register(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return CloudStorageAccount.Parse(options.Value.StorageConnectionString);
                })
                .Keyed<CloudStorageAccount>(key);

#if NETFRAMEWORK
            containerBuilder
                .Register<IStorageFactory>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return new AzureStorageFactory(
                        c.ResolveKeyed<CloudStorageAccount>(key),
                        options.Value.StorageContainer,
                        maxExecutionTime: AzureStorage.DefaultMaxExecutionTime,
                        serverTimeout: AzureStorage.DefaultServerTimeout,
                        path: options.Value.NormalizeStoragePath(),
                        baseAddress: null,
                        useServerSideCopy: true,
                        compressContent: false,
                        verbose: true,
                        initializeContainer: false,
                        throttle: NullThrottle.Instance);
                })
                .Keyed<IStorageFactory>(key);
#endif

            containerBuilder
                .Register<IBlobContainerBuilder>(c => new BlobContainerBuilder(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<BlobContainerBuilder>>()));

            containerBuilder
                .Register<IDownloadDataClient>(c => new DownloadDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<DownloadDataClient>>()));

            containerBuilder
                .Register<IVerifiedPackagesDataClient>(c => new VerifiedPackagesDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<VerifiedPackagesDataClient>>()));

            containerBuilder
                .Register<IOwnerDataClient>(c => new OwnerDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<OwnerDataClient>>()));

            containerBuilder
                .Register<IPopularityTransferDataClient>(c => new PopularityTransferDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<PopularityTransferDataClient>>()));

            containerBuilder
                .Register(c => new Catalog2AzureSearchCommand(
                    c.Resolve<ICollector>(),
                    c.ResolveKeyed<IStorageFactory>(key),
                    c.Resolve<Func<HttpMessageHandler>>(),
                    c.Resolve<IBlobContainerBuilder>(),
                    c.Resolve<IIndexBuilder>(),
                    c.Resolve<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<Catalog2AzureSearchCommand>>()));

            containerBuilder
                .Register(c => new Db2AzureSearchCommand(
                    c.Resolve<INewPackageRegistrationProducer>(),
                    c.Resolve<IPackageEntityIndexActionBuilder>(),
                    c.Resolve<IBlobContainerBuilder>(),
                    c.Resolve<IIndexBuilder>(),
                    c.Resolve<Func<IBatchPusher>>(),
                    c.Resolve<ICatalogClient>(),
                    c.ResolveKeyed<IStorageFactory>(key),
                    c.Resolve<IOwnerDataClient>(),
                    c.Resolve<IDownloadDataClient>(),
                    c.Resolve<IVerifiedPackagesDataClient>(),
                    c.Resolve<IPopularityTransferDataClient>(),
                    c.Resolve<IOptionsSnapshot<Db2AzureSearchConfiguration>>(),
                    c.Resolve<IOptionsSnapshot<Db2AzureSearchDevelopmentConfiguration>>(),
                    c.Resolve<ILogger<Db2AzureSearchCommand>>()));
        }

        private static void RegisterAuxiliaryDataStorageServices(ContainerBuilder containerBuilder, string key)
        {
            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AuxiliaryDataStorageConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.AuxiliaryDataStorageConnectionString,
                        DefaultBlobRequestOptions.Create());
                })
                .Keyed<ICloudBlobClient>(key);

            containerBuilder
                .Register<IAuxiliaryFileClient>(c => new AuxiliaryFileClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AuxiliaryDataStorageConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<AuxiliaryFileClient>>()));
        }

        public static IServiceCollection AddAzureSearch(
            this IServiceCollection services,
            IDictionary<string, string> telemetryGlobalDimensions)
        {
            services.AddV3(telemetryGlobalDimensions);

            services.AddFeatureFlags();
            services.AddTransient<IFeatureFlagService, FeatureFlagService>();

            services.AddTransient<ISearchServiceClientWrapper>(p => new SearchServiceClientWrapper(
                p.GetRequiredService<ISearchServiceClient>(),
                GetSearchDelegatingHandlers(p.GetRequiredService<ILoggerFactory>()),
                GetSearchRetryPolicy(),
                p.GetRequiredService<ILogger<DocumentsOperationsWrapper>>()));

            services
                .AddTransient<ISearchServiceClient>(p =>
                {
                    var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();

                    var client = new SearchServiceClient(
                        options.Value.SearchServiceName,
                        new SearchCredentials(options.Value.SearchServiceApiKey),
#if NETFRAMEWORK
                        new WebRequestHandler(),
#else
                        new HttpClientHandler(),
#endif
                        GetSearchDelegatingHandlers(p.GetRequiredService<ILoggerFactory>()));

                    client.SetRetryPolicy(GetSearchRetryPolicy());

                    return client;
                });

            services.AddSingleton<IAuxiliaryDataCache, AuxiliaryDataCache>();
            services.AddScoped(p => p.GetRequiredService<IAuxiliaryDataCache>().Get());
            services.AddSingleton<IAuxiliaryFileReloader, AuxiliaryFileReloader>();

            services.AddSingleton<ISecretRefresher, SecretRefresher>();

            services.AddTransient<UpdateVerifiedPackagesCommand>();
            services.AddTransient<UpdateDownloadsCommand>();
            services.AddTransient<UpdateOwnersCommand>();
            services.AddTransient(p => new Auxiliary2AzureSearchCommand(
                p.GetRequiredService<UpdateVerifiedPackagesCommand>(),
                p.GetRequiredService<UpdateDownloadsCommand>(),
                p.GetRequiredService<UpdateOwnersCommand>(),
                p.GetRequiredService<IAzureSearchTelemetryService>(),
                p.GetRequiredService<ILogger<Auxiliary2AzureSearchCommand>>()));

            services.AddTransient<IAzureSearchTelemetryService, AzureSearchTelemetryService>();
            services.AddTransient<IBaseDocumentBuilder, BaseDocumentBuilder>();
            services.AddTransient<ICatalogIndexActionBuilder, CatalogIndexActionBuilder>();
            services.AddTransient<ICatalogLeafFetcher, CatalogLeafFetcher>();
            services.AddTransient<ICommitCollectorLogic, AzureSearchCollectorLogic>();
            services.AddTransient<IDatabaseAuxiliaryDataFetcher, DatabaseAuxiliaryDataFetcher>();
            services.AddTransient<IDataSetComparer, DataSetComparer>();
            services.AddTransient<IDocumentFixUpEvaluator, DocumentFixUpEvaluator>();
            services.AddTransient<IDownloadSetComparer, DownloadSetComparer>();
            services.AddTransient<IDownloadTransferrer, DownloadTransferrer>();
            services.AddTransient<IEntitiesContextFactory, EntitiesContextFactory>();
            services.AddTransient<IHijackDocumentBuilder, HijackDocumentBuilder>();
            services.AddTransient<IIndexBuilder, IndexBuilder>();
            services.AddTransient<IIndexOperationBuilder, IndexOperationBuilder>();
            services.AddTransient<INewPackageRegistrationProducer, NewPackageRegistrationProducer>();
            services.AddTransient<IPackageEntityIndexActionBuilder, PackageEntityIndexActionBuilder>();
            services.AddTransient<ISearchDocumentBuilder, SearchDocumentBuilder>();
            services.AddTransient<ISearchIndexActionBuilder, SearchIndexActionBuilder>();
            services.AddTransient<ISearchParametersBuilder, SearchParametersBuilder>();
            services.AddTransient<ISearchResponseBuilder, SearchResponseBuilder>();
            services.AddTransient<ISearchTextBuilder, SearchTextBuilder>();
            services.AddTransient<IServiceClientTracingInterceptor, ServiceClientTracingLogger>();
            services.AddTransient<ISystemTime, SystemTime>();

            return services;
        }

        /// <summary>
        /// Defaults originally taken from:
        /// https://github.com/Azure/azure-sdk-for-net/blob/96421089bc26198098f320ea50e0208e98376956/sdk/mgmtcommon/ClientRuntime/ClientRuntime/RetryDelegatingHandler.cs#L19-L22
        /// 
        /// Note that this policy only applied to the <see cref="RetryDelegatingHandler"/> automatically initialized by
        /// the Azure Search SDK. This policy does not apply to <see cref="WebExceptionRetryDelegatingHandler"/>.
        /// </summary>
        private static RetryPolicy GetSearchRetryPolicy()
        {
            return new RetryPolicy(
                new HttpStatusCodeErrorDetectionStrategy(),
                retryCount: 3,
                minBackoff: TimeSpan.FromSeconds(1),
                maxBackoff: TimeSpan.FromSeconds(10),
                deltaBackoff: TimeSpan.FromSeconds(10));
        }

        public static DelegatingHandler[] GetSearchDelegatingHandlers(ILoggerFactory loggerFactory)
        {
            return new[]
            {
                new WebExceptionRetryDelegatingHandler(loggerFactory.CreateLogger<WebExceptionRetryDelegatingHandler>()),
            };
        }
    }
}
