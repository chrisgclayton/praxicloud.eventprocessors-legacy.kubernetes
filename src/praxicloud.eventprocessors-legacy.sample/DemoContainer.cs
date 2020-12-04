// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.sample
{
    #region Using Clauses
    using Microsoft.Azure.EventHubs;
    using praxicloud.core.kubernetes;
    using praxicloud.eventprocessors_legacy.kubernetes;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// A demo container that processes events from and Event Hub endpoint using the legacy framework
    /// </summary>
    public sealed class DemoContainer : LegacyProcessorBase<DemoProcessor>
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="containerName">The name of the container to use in logging operations</param>
        /// <param name="diagnosticsConfiguration">A diagnostics instance to use when instantiating the container</param>
        /// <param name="probeConfiguration">The availability and liveness probe configuration</param>
        /// <param name="startPosition">The event position to start processing if no checkpoint is found for the partition</param>
        public DemoContainer(string containerName, DiagnosticsConfiguration diagnosticsConfiguration, IProbeConfiguration probeConfiguration, EventPosition startPosition) : base(containerName, diagnosticsConfiguration, probeConfiguration, startPosition)
        {
        }
        #endregion
        #region Methods
        /// <inheritdoc />
        protected override Task<string> GetEventHubConnectionStringAsync()
        {
            return ProcessorUtilities.GetEventHubConnectionStringAsync(Logger);
        }

        /// <inheritdoc />
        protected override Task<string> GetStorageConnectionStringAsync()
        {
            return ProcessorUtilities.GetStorageConnectionStringAsync(Logger);
        }
        #endregion
    }
}
