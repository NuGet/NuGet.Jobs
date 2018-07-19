// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace Validation.Symbols
{
    public class Job : SubcriptionProcessorJob<SymbolValidatorMessage>
    {
        private const string SymbolsConfigurationSectionName = "SymbolsConfiguration";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<SymbolValidatorConfiguration>(configurationRoot.GetSection(SymbolsConfigurationSectionName));
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();
            services.AddTransient<IBrokeredMessageSerializer<SymbolValidatorMessage>, SymbolValidatorMessageSerializer>();
            services.AddTransient<IMessageHandler<SymbolValidatorMessage>, SymbolValidatorMessageHandler>();
            services.AddTransient<ISymbolValidatorService, SymbolValidatorService>();
            services.AddSingleton<ISymbolFileService>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<SymbolValidatorConfiguration>>();
                var packageStorageService = new CloudBlobCoreFileStorageService(new CloudBlobClientWrapper(
                    configurationAccessor.Value.PackageConnectionString,
                    readAccessGeoRedundant: false), c.GetRequiredService<IDiagnosticsService>());


                var packageValidationStorageService = new CloudBlobCoreFileStorageService(new CloudBlobClientWrapper(
                    configurationAccessor.Value.ValidationPackageConnectionString,
                    readAccessGeoRedundant: false), c.GetRequiredService<IDiagnosticsService>());

                var symbolValidationStorageService = new CloudBlobCoreFileStorageService(new CloudBlobClientWrapper(
                   configurationAccessor.Value.ValidationSymbolsConnectionString,
                   readAccessGeoRedundant: false), c.GetRequiredService<IDiagnosticsService>());

                return new SymbolFileService(packageStorageService, packageValidationStorageService, symbolValidationStorageService);
            });
            services.AddSingleton(new TelemetryClient());
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);

            containerBuilder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.SymbolValidator)
                .As<IValidatorStateService>();
        }
    }
}