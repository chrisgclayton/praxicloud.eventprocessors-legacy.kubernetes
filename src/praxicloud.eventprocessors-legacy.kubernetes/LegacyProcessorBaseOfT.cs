// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.kubernetes
{
    #region Using Clauses
    using System;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;
    using praxicloud.core.kubernetes;
    #endregion

    /// <summary>
    /// A sample container that demonstrates a simple container that is initiated and provides logging, metrics, availability, health probes, scale notifications etc.
    /// </summary>
    public abstract class LegacyProcessorBase<T> : LegacyProcessorBase where T : IEventProcessor
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="containerName">The name of the container to use in logging operations</param>
        /// <param name="diagnosticsConfiguration">A diagnostics instance to use when instantiating the container</param>
        /// <param name="probeConfiguration">The availability and liveness probe configuration</param>
        /// <param name="startPosition">The event position to start processing if no checkpoint is found for the partition</param>
        protected LegacyProcessorBase(string containerName, DiagnosticsConfiguration diagnosticsConfiguration, IProbeConfiguration probeConfiguration, EventPosition startPosition) : base(containerName, diagnosticsConfiguration, probeConfiguration, startPosition)
        {
        }
        #endregion
        #region Methods
        /// <summary>
        /// Factory method to create an event processor 
        /// </summary>
        /// <param name="context">The partition context</param>
        /// <returns>The event processor</returns>
        public override IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            return (T)Activator.CreateInstance(typeof(T), context, LoggerFactory, MetricFactory);
        }
        #endregion
    }
}
