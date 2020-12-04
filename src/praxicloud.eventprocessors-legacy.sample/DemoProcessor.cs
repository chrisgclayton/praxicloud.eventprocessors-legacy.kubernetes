// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.sample
{
    #region Using Clauses
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using praxicloud.core.metrics;
    #endregion

    /// <summary>
    /// An event processor host used for testing the lease and checkpoint managers
    /// </summary>
    public sealed class DemoProcessor : IEventProcessor
    {
        #region Constants
        /// <summary>
        /// The interval that the receive events is recorded at (after this number events)
        /// </summary>
        private const int ReportInterval = 10000;
        #endregion
        #region Variables
        /// <summary>
        /// The number of events that have been received
        /// </summary>
        private long _eventCounter = 0;

        /// <summary>
        /// The next report count to report at
        /// </summary>
        private long _nextReport = 1;

        /// <summary>
        /// The last time the reporting was performed
        /// </summary>
        private DateTime _lastReportTime = DateTime.UtcNow;

        /// <summary>
        /// A logger that can be used to write debugging and diagnostics details to
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// A counter used to track the number of messages that have been processed
        /// </summary>
        private readonly ICounter _processedCounter;
        #endregion

        public DemoProcessor(PartitionContext partitionContext, ILoggerFactory loggerFactory, IMetricFactory metricFactory)
        {
            _logger = loggerFactory.CreateLogger("DemoProcessor");
            _processedCounter = metricFactory.CreateCounter("dp_message_count", "The number of messages processed since the processor started", false, new string[0]);
        }

        #region Methods
        /// <inheritdoc />
        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            _logger.LogWarning("Close partition {partitionId}", context.PartitionId);

            if (reason == CloseReason.Shutdown)
            {
                _logger.LogWarning("Reason shutdown");
                await context.CheckpointAsync().ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("Reason other");
            }
        }

        /// <inheritdoc />
        public Task OpenAsync(PartitionContext context)
        {
            _logger.LogWarning("Open partition {partitionId}", context.PartitionId);

            _lastReportTime = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            _logger.LogError(error, "Error processing {partitionId}", context.PartitionId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            _logger.LogDebug("Received events {partitionId}", context.PartitionId);

            if (messages != null)
            {
                var messageList = messages.ToArray();
                _eventCounter += messageList.Length;

                if(messageList.Length > 0) _processedCounter.IncrementBy(messageList.Length);

                if (_eventCounter >= _nextReport)
                {
                    _nextReport = _eventCounter + ReportInterval;

                    await context.CheckpointAsync().ConfigureAwait(false);

                    var currentTime = DateTime.UtcNow;
                    var delta = currentTime - _lastReportTime;

                    _lastReportTime = currentTime;

                    _logger.LogInformation("[{timeSinceMS}] processed {counter} on partition {partitionId}", delta.TotalMilliseconds, _eventCounter, context.PartitionId);

                    await context.CheckpointAsync().ConfigureAwait(false);                    
                }
                else if (messageList.Length < 1)
                {
                    _logger.LogWarning("1- No new messages received on partition {partitionId}, total: {totalMessages}", context.PartitionId, _eventCounter);

                    await context.CheckpointAsync().ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogWarning("2- No new messages received on partition {partitionId}, total: {totalMessages}", context.PartitionId, _eventCounter);

                await context.CheckpointAsync().ConfigureAwait(false);
            }
        }
        #endregion
    }
}


