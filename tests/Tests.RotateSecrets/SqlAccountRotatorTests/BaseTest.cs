using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;
using RotateSecrets.SecretsToRotate;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class BaseTest
    {
        private static readonly object _locker = new object();

        protected BaseTest()
        {
            lock (_locker)
            {
                if (JobRunner.ServiceContainer.GetService<ILoggerFactory>() != null)
                {
                    return;
                }

                // Add an ILoggerFactory to DI to emulate Job.
                var loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
                JobRunner.ServiceContainer.AddService(loggerFactory);
            }
        }

        public static IEnumerable<object[]> BothRanksData => new[]
        {
            new object[] {SqlAccountSecret.Rank.Primary},
            new object[] {SqlAccountSecret.Rank.Secondary}
        };
    }
}
