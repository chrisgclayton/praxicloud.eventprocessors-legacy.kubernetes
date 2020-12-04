// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.kubernetes
{
    #region Using Clauses
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using praxicloud.core.exceptions;
    using praxicloud.core.kubernetes;
    using praxicloud.core.metrics;
    using praxicloud.eventprocessors;
    using praxicloud.eventprocessors.legacy.checkpoints;
    using praxicloud.eventprocessors.legacy.leases;
    #endregion

    /// <summary>
    /// A sample container that demonstrates a simple container that is initiated and provides logging, metrics, availability, health probes, scale notifications etc.
    /// </summary>
    public abstract class LegacyProcessorBase : ContainerBase, IEventProcessorFactory
    {
        #region Constants
        /// <summary>
        /// The name of the partition manager logger
        /// </summary>
        public const string PartitionManagerLoggerName = "PartitionManager";

        /// <summary>
        /// The name of the epoch recorder logger
        /// </summary>
        public const string EpochRecorderLoggerName = "EpochRecorder";

        /// <summary>
        /// The name of the lease manager logger
        /// </summary>
        public const string LeaseManagerLoggerName = "LeaseManager";

        /// <summary>
        /// The name of the checkpoint manager logger
        /// </summary>
        public const string CheckpointManagerLoggerName = "CheckpointManager";

        /// <summary>
        /// The default logger name
        /// </summary>
        public const string DefaultLoggerName = "Default";
        #endregion
        #region Variables
        /// <summary>
        /// A metric that records the number of iterations that have occurred since the start of the container
        /// </summary>
        private readonly ICounter _iterationCounter;

        /// <summary>
        /// A metric that tracks the number of times a scale operation has occurred
        /// </summary>
        private readonly ICounter _scaleEventCounter;

        /// <summary>
        /// Counts the number of times the health check has been invoked since the container started
        /// </summary>
        private readonly ICounter _healthProbeCounter;

        /// <summary>
        /// Counts the number of times the health check has been invoked and failed since the container started
        /// </summary>
        private readonly ICounter _healthProbeFailureCounter;

        /// <summary>
        /// Counts the number of times the availability check has been invoked since the container started
        /// </summary>
        private readonly ICounter _availabilityProbeCounter;

        /// <summary>
        /// Counts the number of times the availability check has been invoked and failed since the container started
        /// </summary>
        private readonly ICounter _availabilityProbeFailureCounter;

        /// <summary>
        /// The partition manager used for Event Hub management
        /// </summary>
        private FixedPartitionManager _partitionManager = null;

        /// <summary>
        /// The factory to build loggers from for subcomponents
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// The event position to start processing if no checkpoint is found for the partition
        /// </summary>
        private readonly EventPosition _startPosition;

        #endregion
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="containerName">The name of the container to use in logging operations</param>
        /// <param name="diagnosticsConfiguration">A diagnostics instance to use when instantiating the container</param>
        /// <param name="probeConfiguration">The availability and liveness probe configuration</param>
        /// <param name="startPosition">The event position to start processing if no checkpoint is found for the partition</param>
        protected LegacyProcessorBase(string containerName, DiagnosticsConfiguration diagnosticsConfiguration, IProbeConfiguration probeConfiguration, EventPosition startPosition) : base(containerName, probeConfiguration, diagnosticsConfiguration)
        {
            _loggerFactory = diagnosticsConfiguration.LoggerFactory;
            _startPosition = startPosition;
            _iterationCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_iteration_counter", "Counts the number of iterations the demo container has performed.", false, new string[0]);
            _scaleEventCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_scale_event_counter", "Counts the number of times a scale event has occurred.", false, new string[0]);
            _healthProbeCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_health_counter", "Counts the number of times the liveness probe endpoint has been accessed since the start of the container.", false, new string[0]);
            _healthProbeFailureCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_health_failure_counter", "Counts the number of times the liveness probe endpoint has been accessed and failed since the start of the container.", false, new string[0]);
            _availabilityProbeCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_availability_counter", "Counts the number of times the readiness probe endpoint has been accessed since the start of the container.", false, new string[0]);
            _availabilityProbeFailureCounter = diagnosticsConfiguration.MetricFactory.CreateCounter("pro_availability_failure_counter", "Counts the number of times the readiness probe endpoint has been accessed and failed since the start of the container.", false, new string[0]);
        }
        #endregion
        #region Properties
        /// <inheritdoc />
        public override bool NotifyOfScaleEvents => true;

        /// <summary>
        /// Returns true if the container should be considered healthy for probe responses
        /// </summary>
        /// <returns>True if healthy</returns>
        protected virtual bool IsContainerHealthy { get; set; } = true;

        /// <summary>
        /// Returns true if the container should be considered available for probe responses
        /// </summary>
        /// <returns>True if available</returns>
        protected virtual bool IsContainerAvailable { get; set; } = true;
        #endregion
        #region Methods
        /// <inheritdoc />
        public override Task<bool> IsAvailableAsync()
        {
            bool isAvailable = IsContainerAvailable; 

            using (Logger.BeginScope("Checking Availability"))
            {
                _availabilityProbeCounter.Increment();

                if (isAvailable)
                {                    
                    Logger.LogDebug("Availability test for legacy even processor instance with StatefulSet index {statefulSetIndexCount} was true", KubernetesStatefulSetIndex);
                }
                else
                {
                    _availabilityProbeFailureCounter.Increment();
                    Logger.LogWarning("Availability test for legacy even processor instance with StatefulSet index {statefulSetIndexCount} was false", KubernetesStatefulSetIndex);
                }
            }

            return Task.FromResult(isAvailable);
        }

        /// <inheritdoc />
        public override Task<bool> IsHealthyAsync()
        {
            bool isHealthy = IsContainerHealthy;

            using (Logger.BeginScope("Checking Health"))
            {
                _healthProbeCounter.Increment();

                if (isHealthy)
                {
                    Logger.LogDebug("Health test for legacy even processor instance with StatefulSet index {statefulSetIndexCount} was true", KubernetesStatefulSetIndex);
                }
                else
                {
                    _healthProbeFailureCounter.Increment();
                    Logger.LogWarning("Health test for legacy even processor instance with StatefulSet index {statefulSetIndexCount} was false", KubernetesStatefulSetIndex);
                }
            }

            return Task.FromResult(isHealthy);
        }

        /// <inheritdoc />
        protected sealed override void KubernetesReplicaChange(int? previousReplicaCount, int? previousDesiredReplicaCount, int? previousReadyReplicaCount, int? newReplicaCount, int? newDesiredReplicaCount, int? newReadyReplicaCount)
        {
            var previousCount = previousDesiredReplicaCount ?? -1;
            var newCount = newDesiredReplicaCount ?? -1;

            using (Logger.BeginScope("Replica Change Notification"))
            {
                Logger.LogInformation("Controller instances change received for legacy even processor instance with StatefulSet index {statefulSetIndexCount} with a new count of {desiredReplicaCount} and previous count of {previousDesiredReplicaCount}", KubernetesStatefulSetIndex, newCount, previousCount);

                if (previousCount > 0 && previousCount != newCount)
                {
                    Logger.LogWarning("The desired number of replica change was received for legacy even processor instance with StatefulSet index {statefulSetIndexCount} with a new count of {desiredReplicaCount} and previous count of {previousDesiredReplicaCount}", KubernetesStatefulSetIndex, newCount, previousCount);
                    _scaleEventCounter.Increment();

                    HandleReplicaChangeAsync(previousCount, newCount).GetAwaiter().GetResult();
                }
            }
        }

        /// <summary>
        /// Asynchronous replica handler used due to multiple async calls
        /// </summary>
        /// <param name="previousReplicaCount">The previous desired replica nummber</param>
        /// <param name="newReplicaCount">The new desired replica count</param>
        private async Task HandleReplicaChangeAsync(int previousReplicaCount, int newReplicaCount)
        {
            await _partitionManager.UpdateManagerQuantityAsync(newReplicaCount, ContainerCancellationToken).ConfigureAwait(false);
            await HandleScaleEventAsync(previousReplicaCount, newReplicaCount).ConfigureAwait(false);
        }

        /// <summary>
        /// Provided for derived types to inherit from when a scale event occurs
        /// </summary>
        /// <param name="previousReplicaCount">The previous desired replica nummber</param>
        /// <param name="newReplicaCount">The new desired replica count</param>
        protected virtual Task HandleScaleEventAsync(int previousReplicaCount, int newReplicaCount)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task StartupAsync(CancellationToken cancellationToken)
        {
            Logger.LogWarning("Starting legacy even processor instance with StatefulSet index {statefulSetIndexCount}", KubernetesStatefulSetIndex);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task ShutdownAsync(bool startupSuccessful, CancellationToken cancellationToken)
        {
            Logger.LogWarning("Shutting down legacy even processor instance with StatefulSet index {statefulSetIndexCount}", KubernetesStatefulSetIndex);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override bool UnhandledExceptionRaised(object sender, Exception exception, bool isTerminating, UnobservedType sourceType)
        {
            Logger.LogError(exception, "An unhandled exception of type {exceptionType} was caught in legacy even processor instance with StatefulSet index {statefulSetIndexCount} with a terminating value of {isTerminating}", sourceType, KubernetesStatefulSetIndex, isTerminating);

            return false;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Started execution loop");

            // Once the initial tasks are completed indicate the service is now available
            IsContainerAvailable = true;

            var checkpointStoreRoot = Environment.GetEnvironmentVariable("CheckpointStore");
            var eventHubConnectionString = await GetEventHubConnectionStringAsync().ConfigureAwait(false);
            var checkpointConnectionString = await GetStorageConnectionStringAsync().ConfigureAwait(false);
            var consumerGroupName = Environment.GetEnvironmentVariable("ConsumerGroup");
            var checkpointContainerName = Environment.GetEnvironmentVariable("CheckpointContainerName");
            var epochContainerName = Environment.GetEnvironmentVariable("EpochContainerName");
            var eventProcessorHostName = Environment.GetEnvironmentVariable("EventProcessorHostName");


            if (!int.TryParse(Environment.GetEnvironmentVariable("LeaseDurationSeconds"), out int leaseDurationSeconds) || leaseDurationSeconds < 20) leaseDurationSeconds = 20;
            if (!int.TryParse(Environment.GetEnvironmentVariable("LeaseRenewalSeconds"), out int leaseRenewalSeconds) || leaseRenewalSeconds < 10 || leaseRenewalSeconds >= leaseDurationSeconds) leaseRenewalSeconds = 10;
            if (!int.TryParse(Environment.GetEnvironmentVariable("ReceiveTimeoutSeconds"), out int receiveTimeout) || receiveTimeout < 5) receiveTimeout = 60;
            if (!int.TryParse(Environment.GetEnvironmentVariable("PrefetchCount"), out int prefetchCount) || prefetchCount < 0) prefetchCount = 0;
            if (!int.TryParse(Environment.GetEnvironmentVariable("MaxBatchSize"), out int maxBatchSize) || maxBatchSize < 1) maxBatchSize = 1;
            if (!int.TryParse(Environment.GetEnvironmentVariable("IterationSeconds"), out int iterationSeconds) || iterationSeconds < 1) iterationSeconds = 1;

            var iterationDelay = TimeSpan.FromSeconds(iterationSeconds);

            Logger.LogInformation("Creating fixed partition manager");
            _partitionManager = new FixedPartitionManager(_loggerFactory.CreateLogger(PartitionManagerLoggerName), eventHubConnectionString, KubernetesStatefulSetIndex.Value);

            Logger.LogInformation("Initializing fixed partition manager");
            await _partitionManager.InitializeAsync(KubernetesDesiredReplicaCount.Value).ConfigureAwait(true);

            Logger.LogInformation("Creating epoch reader");
            var epochRecorder = new AzureStorageEpochRecorder(_loggerFactory.CreateLogger(EpochRecorderLoggerName), MetricFactory, consumerGroupName, checkpointConnectionString, epochContainerName, null);

            Logger.LogInformation("Creating fixed lease manager");
            var leaseManager = new FixedLeaseManager(_loggerFactory.CreateLogger(LeaseManagerLoggerName), _partitionManager, epochRecorder);

            Logger.LogInformation("Creating Azure storage checkpoint manager");
            var checkpointManager = new AzureStorageCheckpointManager(_loggerFactory.CreateLogger(CheckpointManagerLoggerName), MetricFactory, checkpointConnectionString, checkpointContainerName, null);

            Logger.LogInformation("Building connection string");
            var builder = new EventHubsConnectionStringBuilder(eventHubConnectionString);

            Logger.LogInformation("Creating event processor host");
            var host = new EventProcessorHost(eventProcessorHostName, builder.EntityPath, consumerGroupName, builder.ToString(), checkpointManager, leaseManager)
            {
                PartitionManagerOptions = new PartitionManagerOptions
                {
                    RenewInterval = TimeSpan.FromSeconds(leaseRenewalSeconds),
                    LeaseDuration = TimeSpan.FromSeconds(leaseDurationSeconds)
                }
            };

            Logger.LogInformation("Initializing checkpoint manager");
            checkpointManager.Initialize(host);

            Logger.LogInformation("Initializing lease manager");
            await leaseManager.InitializeAsync(host).ConfigureAwait(true);

            Logger.LogInformation("Receive timeout is set to: {receiveTimeout} seconds", receiveTimeout);

            var processorOptions = new EventProcessorOptions
            {
                InitialOffsetProvider = (partitionId) => _startPosition,
                InvokeProcessorAfterReceiveTimeout = true,
                MaxBatchSize = maxBatchSize,
                PrefetchCount = prefetchCount,
                ReceiveTimeout = TimeSpan.FromSeconds(receiveTimeout)
            };

            Logger.LogInformation("Registering event processor host");

            await host.RegisterEventProcessorFactoryAsync(this, processorOptions).ConfigureAwait(false);

            Logger.LogInformation("Starting loop");

            while (!ContainerCancellationToken.IsCancellationRequested)
            {
                await Task.WhenAny(Task.Delay(iterationDelay), ContainerTask).ConfigureAwait(false);

                _iterationCounter.Increment();
                Logger.LogDebug("Derived Iteration completed");
            }

            Logger.LogWarning("Unregistering event processory");
            await host.UnregisterEventProcessorAsync().ConfigureAwait(true);


            // Service should be flagged as no longer available before any cleanup tasks
            IsContainerAvailable = false;

            Logger.LogWarning("Ending execution loop");

        }

        /// <summary>
        /// Retrieves the event hub connection string
        /// </summary>
        /// <returns></returns>
        protected abstract Task<string> GetEventHubConnectionStringAsync();

        /// <summary>
        /// Retrieves the storage connection string
        /// </summary>
        /// <returns></returns>
        protected abstract Task<string> GetStorageConnectionStringAsync();

        /// <inheritdoc />
        public abstract IEventProcessor CreateEventProcessor(PartitionContext context);
        #endregion
    }
}
