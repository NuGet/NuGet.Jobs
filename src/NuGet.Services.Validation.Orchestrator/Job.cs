// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.PackageSigning;
using NuGet.Services.Validation.Vcs;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string VcsSectionName = "Vcs";
        private const string ValidateOnlyConfigurationKey = nameof(ValidateOnlyConfiguration.ValidateOnly);

        private const string VcsBindingKey = VcsSectionName;
        private const string PackageVerificationTopicClientBindingKey = "PackageVerificationTopicClient";

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
        }

        public override Task Run()
        {
            var validator = GetRequiredService<ConfigurationValidator>();
            validator.Validate();
            ConfigurationValidated = true;

            return Task.FromResult(0);
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename)
                .AddInMemoryCollection(new[] { new KeyValuePair<string, string>(ValidateOnlyConfigurationKey, validateOnly.ToString()) });

            var unprocessedConfiguration = builder.Build();

            if (_validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return unprocessedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(unprocessedConfiguration);
            return new SecretConfigurationReader(unprocessedConfiguration, secretReaderFactory);
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
            services.AddOptions();
            services.AddSingleton(this.LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<VcsConfiguration>(configurationRoot.GetSection(VcsSectionName));
            services.Configure<ValidateOnlyConfiguration>(configurationRoot);
            services.AddTransient<ConfigurationValidator>();
            services.AddTransient<VcsValidator>();
            services.AddTransient<IPackageSignatureVerificationEnqueuer, PackageSignatureVerificationEnqueuer>();
            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IValidatorStateService, ValidatorStateService>();
            services.AddTransient<PackageSigningValidator>();
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
                .Register(c =>
                {
                    var serviceBusConfiguration = c.Resolve<IOptionsSnapshot<ServiceBusConfiguration>>();
                    var topicClient = new TopicClientWrapper(serviceBusConfiguration.Value.ConnectionString, serviceBusConfiguration.Value.TopicPath);
                    return topicClient;
                })
                .Keyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey);

            containerBuilder
                .RegisterType<PackageValidationService>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(CloudStorageAccount),
                    (pi, ctx) => ctx.ResolveKeyed<CloudStorageAccount>(VcsBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationService>();

            containerBuilder
                .RegisterType<PackageValidationAuditor>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(CloudStorageAccount),
                    (pi, ctx) => ctx.ResolveKeyed<CloudStorageAccount>(VcsBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationAuditor>();

            containerBuilder
                .RegisterType<PackageSignatureVerificationEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IBrokeredMessageSerializer<SignatureValidationMessage>),
                    (pi, ctx) => ctx.Resolve<SignatureValidationMessageSerializer>()
                    ))
                .As<IPackageSignatureVerificationEnqueuer>();

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
