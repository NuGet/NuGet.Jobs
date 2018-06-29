// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation
{
    public abstract class SubcriptionProcessorJob<T> : JsonConfigurationJob
    {
        /// <summary>
        /// The maximum amount of time that graceful shutdown can take before the job will
        /// forcefully end itself.
        /// </summary>
        private static readonly TimeSpan MaxShutdownTime = TimeSpan.FromMinutes(1);

        public override async Task Run()
        {
            var processor = _serviceProvider.GetRequiredService<ISubscriptionProcessor<T>>();

            processor.Start();

            // Wait a day, and then shutdown this process so that it is restarted.
            await Task.Delay(TimeSpan.FromDays(1));

            if (!await processor.ShutdownAsync(MaxShutdownTime))
            {
                Logger.LogWarning(
                    "Failed to gracefully shutdown Service Bus subscription processor. {MessagesInProgress} messages left",
                    processor.NumberOfMessagesInProgress);
            }
        }

        internal override void ConfigureAutofacServicesInternal(ContainerBuilder containerBuilder)
        {
            const string validateCertificateBindingKey = "SubscriptionProcessorJob_SubscriptionProcessorKey";

            containerBuilder
                .RegisterType<ScopedMessageHandler<T>>()
                .Keyed<IMessageHandler<T>>(validateCertificateBindingKey);

            containerBuilder
                .RegisterType<SubscriptionProcessor<T>>()
                .WithParameter(
                    (parameter, context) => parameter.ParameterType == typeof(IMessageHandler<T>),
                    (parameter, context) => context.ResolveKeyed(validateCertificateBindingKey, typeof(IMessageHandler<T>)))
                .As<ISubscriptionProcessor<T>>();
        }
    }
}
