// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Validation.Common;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Vcs;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string VcsSectionName = "Vcs";
        private const string RunnerConfigurationSectionName = "RunnerConfiguration";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string ValidationDbConfiguraitonSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";

        private const string VcsBindingKey = VcsSectionName;
        private const string ValidationStorageBindingKey = "ValidationStorage";
        private const string OrchestratorBindingKey = "Orchestrator";

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private string _configurationFilename;
        private bool _validateOnly;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Indicates whether we had successful configuration validation or not
        /// </summary>
        public bool ConfigurationValidated { get; set; }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            _configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _validateOnly = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, ValidateArgument, defaultValue: false);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(_configurationFilename, _validateOnly));
            ConfigurationValidated = false;

            // Override any custom DB configuration specified for entity context classes with one specific configuration
            // Needed because entity configuration specified through the attribute for the context class MUST exist in
            // the same assembly as the entity class itself AND there can be only one DB configuration per application.
            // In our case we have two entity contexts: Gallery and Validation which both specify their own configuration
            // and hence they cannot coexist without additional setup below.
            //System.Data.Entity.DbConfiguration.Loaded += (_, a) =>
            //{
            //    a.ReplaceService<System.Data.Entity.DbConfiguration>((s, k) => new NuGetGallery.EntitiesConfiguration());
            //};
        }

        public override async Task Run()
        {
            var validator = GetRequiredService<ConfigurationValidator>();
            validator.Validate();
            ConfigurationValidated = true;

            if (_validateOnly)
            {
                Logger.LogInformation("Configuration validation successful");
                return;
            }

            var runner = GetRequiredService<OrchestrationRunner>();
            await runner.RunOrchestrationAsync();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            if (_validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return uninjectedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();
            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<VcsConfiguration>(configurationRoot.GetSection(VcsSectionName));
            services.Configure<OrchestrationRunnerConfiguration>(configurationRoot.GetSection(RunnerConfigurationSectionName));
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfiguraitonSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));

            services.AddTransient<ConfigurationValidator>();
            services.AddTransient<OrchestrationRunner>();

            services.AddScoped<NuGetGallery.IEntitiesContext>(serviceProvider =>
                new NuGetGallery.EntitiesContext(
                    serviceProvider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value.ConnectionString, false));
            services.AddScoped<NuGet.Services.Validation.ValidationEntitiesContext>(serviceProvider =>
                new NuGet.Services.Validation.ValidationEntitiesContext(
                    serviceProvider.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>().Value.ConnectionString));
            services.AddScoped<IValidationStorageService, ValidationStorageService>();
            services.Add(ServiceDescriptor.Transient(typeof(NuGetGallery.IEntityRepository<>), typeof(NuGetGallery.EntityRepository<>)));
            services.AddTransient<NuGetGallery.ICorePackageService, NuGetGallery.CorePackageService>();
            services.AddTransient<ISubscriptionClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new SubscriptionClientWrapper(configuration.ConnectionString, configuration.TopicPath, configuration.SubscriptionName);
            });
            services.AddTransient<ITopicClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
            });
            services.AddTransient<IPackageValidationEnqueuer, PackageValidationEnqueuer>();
            services.AddTransient<IValidatorProvider, ValidatorProvider>();
            services.AddTransient<IServiceScopeProvider, ServiceScopeProvider>();
            services.AddTransient<IValidationSetProvider, ValidationSetProvider>();
            services.AddTransient<IMessageHandler<PackageValidationMessageData>, ValidationMessageHandler>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<PackageValidationMessageData>, PackageValidationMessageDataSerializationAdapter>();
            services.AddTransient<VcsValidator>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            /// Initialize dependencies for the <see cref="VcsValidator"/>. There is some additional complexity here
            /// because the implementations require ambiguous types (such as a <see cref="string"/> and a
            /// <see cref="CloudStorageAccount"/> which there may be more than one configuration of).
            containerBuilder
                .Register(c =>
                {
                    var vcsConfiguration = c.Resolve<IOptionsSnapshot<VcsConfiguration>>();
                    var cloudStorageAccount = CloudStorageAccount.Parse(vcsConfiguration.Value.DataStorageAccount);
                    return cloudStorageAccount;
                })
                .Keyed<CloudStorageAccount>(VcsBindingKey);

            containerBuilder
                .RegisterType<PackageValidationService>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationService>();

            containerBuilder
                .RegisterType<PackageValidationAuditor>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationAuditor>();

            containerBuilder
                .Register(c => 
                {
                    var configurationAccessor = c.Resolve<IOptionsSnapshot<ValidationConfiguration>>();
                    return new NuGetGallery.CloudBlobClientWrapper(configurationAccessor.Value.ValidationStorageConnectionString, false);
                })
                .Keyed<NuGetGallery.ICloudBlobClient>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterKeyedTypeWithKeyedParameter<NuGetGallery.ICoreFileStorageService, NuGetGallery.CloudBlobCoreFileStorageService, NuGetGallery.ICloudBlobClient>(
                    typeKey: ValidationStorageBindingKey,
                    parameterKey: ValidationStorageBindingKey);

            containerBuilder
                .RegisterKeyedTypeWithKeyedParameter<NuGetGallery.ICorePackageFileService, NuGetGallery.CorePackageFileService, NuGetGallery.ICoreFileStorageService>(
                    typeKey: ValidationStorageBindingKey,
                    parameterKey: ValidationStorageBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<IValidationOutcomeProcessor, ValidationOutcomeProcessor, NuGetGallery.ICorePackageFileService>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<IValidationSetProcessor, ValidationSetProcessor, NuGetGallery.ICorePackageFileService>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterType<ScopedPackageValidationMessageHandler>()
                .Keyed<IMessageHandler<PackageValidationMessageData>>(OrchestratorBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    ISubscriptionProcessor<PackageValidationMessageData>, 
                    SubscriptionProcessor<PackageValidationMessageData>, 
                    IMessageHandler<PackageValidationMessageData>>(
                        OrchestratorBindingKey);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
