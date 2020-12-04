// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.kubernetes
{
    using Azure.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using praxicloud.core.kubernetes;
    using praxicloud.core.metrics;
    using praxicloud.core.metrics.applicationinsights;
    using praxicloud.core.metrics.prometheus;
    using praxicloud.core.security;
    using praxicloud.core.security.keyvault;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;

    public static class ProcessorUtilities
    {
        /// <summary>
        /// Determines the default start position for processing when no checkpoint is found
        /// </summary>
        /// <returns>An event position indicating the startup logic</returns>
        public static EventPosition GetStartPosition()
        {
            EventPosition startPosition;
            var startDefinition = Environment.GetEnvironmentVariable("StartPosition");
            if (!bool.TryParse(Environment.GetEnvironmentVariable("StartInclusive"), out var startInclusive)) startInclusive = false;

            if (string.IsNullOrWhiteSpace(startDefinition)) startDefinition = "Start";
            startDefinition = startDefinition.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(startDefinition)) startDefinition = "Start";


            switch (startDefinition)
            {
                case "end":
                    startPosition = EventPosition.FromEnd();
                    break;

                case "offset":
                    startPosition = EventPosition.FromOffset(Environment.GetEnvironmentVariable("StartOffset"), startInclusive);
                    break;

                case "sequence":
                    if (!long.TryParse(Environment.GetEnvironmentVariable("StartSequence"), out var startSequence)) startSequence = -1;
                    startPosition = EventPosition.FromSequenceNumber(startSequence, startInclusive);
                    break;

                case "time":
                    if (!int.TryParse(Environment.GetEnvironmentVariable("StartSeconds"), out var startSeconds)) startSeconds = 0;
                    startPosition = EventPosition.FromEnqueuedTime(DateTime.UtcNow.AddSeconds(startSeconds * -1));
                    break;

                default:
                    startPosition = EventPosition.FromStart();
                    break;
            }


            return startPosition;
        }

        /// <summary>
        /// Retrieves the probe configuration for the health and availability probes
        /// </summary>
        /// <returns>Configuraiton for the health and availability probes</returns>
        public static IProbeConfiguration GetProbeConfiguration()
        {
            if (!bool.TryParse(Environment.GetEnvironmentVariable("UseTcpProbe"), out var useTcpProbe)) useTcpProbe = true;

            var configuraiton = new ProbeConfiguration
            {
                AvailabilityIPAddress = IPAddress.Any,
                AvailabilityProbeInterval = TimeSpan.FromSeconds(5),
                UseTcp = useTcpProbe,
                HealthIPAddress = IPAddress.Any,
                HealthProbeInterval = TimeSpan.FromSeconds(5)
            };

            if (ushort.TryParse(Environment.GetEnvironmentVariable("ReadinessProbePort"), out var availabilityProbePort))
            {
                configuraiton.AvailabilityPort = availabilityProbePort;
            }

            if (ushort.TryParse(Environment.GetEnvironmentVariable("HealthProbePort"), out var healthProbePort))
            {
                configuraiton.HealthPort = healthProbePort;
            }

            return configuraiton;
        }

        public static async Task<string> GetEventHubConnectionStringAsync(ILogger logger)
        {
            string connectionString = null;

            try
            {
                var secretName = Environment.GetEnvironmentVariable("HubSecretName");
                var client = GetSecretsClient(logger);
                var response = await client.GetAsync(secretName).ConfigureAwait(false);
                logger.LogInformation("Retrieved secret");

                if (response.IsSuccess)
                {
                    var secretValue = response.Value;
                    connectionString = SecureStringUtilities.SecureStringToString(secretValue);
                }
                else
                {
                    logger.LogError(response.Exception, "Response Id: {httpCode}", response.HttpStatus);
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error retrieving details from key vault");
            }

            return connectionString;
        }

        public static async Task<string> GetStorageConnectionStringAsync(ILogger logger)
        {
            string connectionString = null;

            try
            {
                var secretName = Environment.GetEnvironmentVariable("StorageSecretName");
                var client = GetSecretsClient(logger);
                var response = await client.GetAsync(secretName).ConfigureAwait(false);
                logger.LogInformation("Retrieved secret");

                if (response.IsSuccess)
                {
                    var secretValue = response.Value;
                    connectionString = SecureStringUtilities.SecureStringToString(secretValue);
                }
                else
                {
                    logger.LogError(response.Exception, "Response Id: {httpCode}", response.HttpStatus);
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error retrieving details from key vault");
            }

            return connectionString;
        }

        public static KeyVaultSecrets GetSecretsClient(ILogger logger)
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            var hubSecretName = Environment.GetEnvironmentVariable("HubSecretName");
            var identityClientId = Environment.GetEnvironmentVariable("IdentityClientId");
            var tenantId = Environment.GetEnvironmentVariable("TenantId");
            var clientId = Environment.GetEnvironmentVariable("ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");

            logger.LogInformation("Retrieving secrets from vault named: {valueName} and a hub secret named: {hubSecretName}", keyVaultName, hubSecretName);

            TokenCredential tokenProvider;

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                tokenProvider = string.IsNullOrWhiteSpace(identityClientId) ? AzureOauthTokenAuthentication.GetOauthTokenCredentialFromManagedIdentity() : AzureOauthTokenAuthentication.GetOauthTokenCredentialFromManagedIdentity(identityClientId);
            }
            else
            {
                tokenProvider = AzureOauthTokenAuthentication.GetOauthTokenCredentialFromClientSecret(tenantId, clientId, clientSecret);
            }

            logger.LogInformation("Completed creation of token provider");

            var vault = new KeyVault(keyVaultName, tokenProvider, 3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10));
            logger.LogInformation("Created key vault");

            return vault.GetSecretsClient();
        }

        /// <summary>
        /// Returns an instance of the metric factory that instrumentation will be built from
        /// </summary>
        /// <returns>A metric factory that metric containers will be constructed from</returns>
        public static IMetricFactory GetMetricFactory()
        {
            var metricFactory = new MetricFactory();

            var prometheusPortValue = Environment.GetEnvironmentVariable("PrometheusPort");

            if (!string.IsNullOrWhiteSpace(prometheusPortValue) && int.TryParse(prometheusPortValue, out int port))
            {
                metricFactory.AddPrometheus("prom", port);
            }

            var applicationInsightsValue = Environment.GetEnvironmentVariable("InstrumentationKey");

            if (!string.IsNullOrWhiteSpace(applicationInsightsValue))
            {
                metricFactory.AddApplicationInsights("app", applicationInsightsValue);
            }

            return metricFactory;
        }

        /// <summary>
        /// Builds the logger factory with associated providers
        /// </summary>
        /// <param name="minimumLogLevel">The minimum log level</param>
        /// <param name="sourceLogLevels">A list of sources that may optionally be provided to indicate log levels for specific logger sources</param>
        /// <returns>An instance of a logger factory</returns>
        public static ILoggerFactory GetLoggerFactory(LogLevel minimumLogLevel = LogLevel.Information, Dictionary<string, LogLevel> sourceLogLevels = null)
        {
            var serviceContainer = new ServiceCollection();

            serviceContainer.AddLogging(configure =>
            {
                configure.ClearProviders();

                MemoryStream sourceValues = null;

                if ((sourceLogLevels?.Count ?? 0) > 0)
                {
                    if (sourceLogLevels == null) sourceLogLevels = new Dictionary<string, LogLevel>();
                    if (!sourceLogLevels.Any(pair => string.Equals(pair.Key, LegacyProcessorBase.DefaultLoggerName, StringComparison.OrdinalIgnoreCase))) sourceLogLevels.Add(LegacyProcessorBase.DefaultLoggerName, minimumLogLevel);
                    var logLevelCollection = sourceLogLevels.Select(pair => new KeyValuePair<string, string>(pair.Key, Enum.GetName(typeof(LogLevel), pair.Value))).ToDictionary(pair => pair.Key, pair => pair.Value);

                    var providerSettings = new LoggingProviderSettings
                    {
                        LogLevel = logLevelCollection
                    };

                    var sourceJson = JsonConvert.SerializeObject(providerSettings);
                    sourceValues = new MemoryStream(Encoding.ASCII.GetBytes(sourceJson));
                }

                var builder = new ConfigurationBuilder();
                if ((sourceValues?.Length ?? 0) > 0) builder.AddJsonStream(sourceValues);
                var defaultLoggingInformation = builder.Build();

                configure.SetMinimumLevel(minimumLogLevel);
                configure.AddConfiguration(defaultLoggingInformation);

                configure.AddConsole(options =>
                {
                    options.DisableColors = true;
                    options.IncludeScopes = false;
                });
            });

            var provider = serviceContainer.BuildServiceProvider();

            return provider.GetRequiredService<ILoggerFactory>();
        }

        /// <summary>
        /// Determines the log levels for the various sources
        /// </summary>
        /// <returns>A dictionary populated with known source levels</returns>
        public static Dictionary<string, LogLevel> GetLoggerSourceLevels(string containerName, LogLevel logLevel)
        {
            var sourceLogLevel = new Dictionary<string, LogLevel>();

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PartitionManagerLogLevel"))) sourceLogLevel.Add(LegacyProcessorBase.PartitionManagerLoggerName, Enum.Parse<LogLevel>(Environment.GetEnvironmentVariable("PartitionManagerLogLevel")));
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EpochRecorderLogLevel"))) sourceLogLevel.Add(LegacyProcessorBase.EpochRecorderLoggerName, Enum.Parse<LogLevel>(Environment.GetEnvironmentVariable("EpochRecorderLogLevel")));
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LeaseManagerLogLevel"))) sourceLogLevel.Add(LegacyProcessorBase.LeaseManagerLoggerName, Enum.Parse<LogLevel>(Environment.GetEnvironmentVariable("LeaseManagerLogLevel")));
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CheckpointManagerLogLevel"))) sourceLogLevel.Add(LegacyProcessorBase.CheckpointManagerLoggerName, Enum.Parse<LogLevel>(Environment.GetEnvironmentVariable("CheckpointManagerLogLevel")));

            sourceLogLevel.Add(containerName, logLevel);

            return sourceLogLevel;
        }

        /// <summary>
        /// Retrieves the logging level that should be performed by default
        /// </summary>
        /// <returns>The log level</returns>
        public static LogLevel GetDefaultLogLevel()
        {
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LogLevel")) ? LogLevel.Information : Enum.Parse<LogLevel>(Environment.GetEnvironmentVariable("LogLevel"));
        }
    }
}
