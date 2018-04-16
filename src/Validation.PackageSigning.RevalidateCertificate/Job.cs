// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class Job : JsonConfigurationJob
    {
        private const string RevalidationConfigurationSectionName = "RevalidateJob";

        private ICertificateRevalidator _revalidator;
        private RevalidationConfiguration _configuration;
        private ILogger<Job> _logger;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(jobArgsDictionary);

            if (!JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once))
            {
                throw new ArgumentException($"Missing {JobArgumentNames.Once}");
            }

            _revalidator = _serviceProvider.GetRequiredService<ICertificateRevalidator>();
            _configuration = _serviceProvider.GetRequiredService<RevalidationConfiguration>();
            _logger = _serviceProvider.GetRequiredService<ILogger<Job>>();
        }

        public override async Task Run()
        {
            var executionTime = Stopwatch.StartNew();

            while (executionTime.Elapsed < _configuration.RestartThreshold)
            {
                // Both of these methods only do a chunk of the possible promotion/revalidating work before
                // completing. This "Run" method may need to run several times to promote all signatures
                // and to revalidate all stale certificates.
                await _revalidator.PromoteSignaturesAsync();
                await _revalidator.RevalidateStaleCertificatesAsync();

                _logger.LogInformation("Sleeping for {SleepDuration}...", _configuration.SleepDuration);

                await Task.Delay(_configuration.SleepDuration);
            }

            _logger.LogInformation(
                "Job has reached configured restart threshold after {ElapsedTime}. Restarting...",
                executionTime.Elapsed);
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(RevalidationConfigurationSectionName));

            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value);

            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ICertificateRevalidator, CertificateRevalidator>();
            services.AddTransient<IValidateCertificateEnqueuer, ValidateCertificateEnqueuer>();
            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();

            services.AddTransient<ITopicClient>(provider =>
            {
                var config = provider.GetRequiredService<ServiceBusConfiguration>();

                return new TopicClientWrapper(config.ConnectionString, config.TopicPath);
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}