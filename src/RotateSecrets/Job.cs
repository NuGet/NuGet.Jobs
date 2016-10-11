using System;
using NuGet.Jobs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Azure.Management.Storage;
using NuGet.Services.Logging;
using RotateSecrets.SecretRotators;
using Serilog.Configuration;

namespace RotateSecrets
{
    internal class Job
        : JobBase
    {
        private ILogger _logger;
        
        private StorageManagementClient _storageManagementClient;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
                JobRunner.ServiceContainer.AddService(loggerFactory);
                _logger = loggerFactory.CreateLogger<Job>();

                var secretLongevityDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary,
                    JobArgumentNames.SecretLongevityDays);
                if (secretLongevityDays.HasValue)
                {
                    SecretRotatorFactory.Instance.SecretLongevityDays = secretLongevityDays.Value;
                }
                
                var subscriptionId = JobConfigurationManager.TryGetArgument(jobArgsDictionary,
                    JobArgumentNames.SubscriptionId);
                var tenantId = JobConfigurationManager.TryGetArgument(jobArgsDictionary,
                    JobArgumentNames.TenantId);
                if (subscriptionId != null)
                {
                    var authContext = new AuthenticationContext($"https://login.windows.net/{tenantId}");
                    var result = authContext.AcquireTokenAsync("https://management.core.windows.net/", Utils.Instance.VaultConfig.GetClientAssertionCertificate()).Result;
                    _storageManagementClient =
                        new StorageManagementClient(new TokenCredentials(result.AccessToken))
                        {
                            SubscriptionId = subscriptionId
                        };
                    JobRunner.ServiceContainer.AddService(_storageManagementClient);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogCritical("Job failed to initialize! {Exception}", ex);

                return false;
            }

            return true;
        }
        
        public override async Task<bool> Run()
        {
            var numTotal = 0;
            var numErrors = 0;
            var numRecovered = 0;
            var numRecent = 0;
            var numSuccess = 0;

            foreach (var secretItem in await Utils.Instance.ListSecrets())
            {
                var secret = await Utils.Instance.GetSecret(secretItem.Identifier.Name);
                try
                {
                    var secretRotator = SecretRotatorFactory.Instance.GetSecretRotator(secret);
                    var result = await secretRotator.ProcessSecret();

                    _logger.LogInformation("{SecretName}: Result: {TaskResult}", 
                        secret.SecretIdentifier.Name, Enum.GetName(typeof(SecretRotator.TaskResult), result));

                    numTotal++;
                    switch (result)
                    {
                        case SecretRotator.TaskResult.Error:
                            numErrors++;
                            break;
                        case SecretRotator.TaskResult.Recovered:
                            numRecovered++;
                            break;
                        case SecretRotator.TaskResult.Recent:
                            numRecent++;
                            break;
                        case SecretRotator.TaskResult.Success:
                            numSuccess++;
                            break;
                        default:
                            // Ignore and intermediate results shouldn't count in the totals.
                            numTotal--;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Failed to rotate secret {SecretName}! {Exception}", secretItem.Identifier.Name, ex);
                }
            }

            if (!SecretRotatorFactory.Instance.IsAllSecretsRotated())
            {
                _logger.LogWarning("Some secrets were marked to be rotated but were not rotated!");
            }
            
            _logger.LogInformation("Processed {numTotal} secrets.", numTotal);

            if (numSuccess > 0)
            {
                _logger.LogInformation("Rotated {numSuccess} secrets successfully.", numSuccess);
            }

            if (numRecovered > 0)
            {
                _logger.LogInformation("Recovered {numRecovered} secrets from an invalid state.", numRecovered);
            }

            if (numRecent > 0)
            {
                _logger.LogInformation("{numFresh} secrets were recently set and do not need to be rotated.", numRecent);
            }

            if (numErrors > 0)
            {
                _logger.LogInformation("Failed to rotate {numErrors} secrets.", numErrors);
            }

            return true;
        }
    }
}
