// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace praxicloud.eventprocessors_legacy.sample
{
    #region Using Clauses
    using praxicloud.core.kubernetes;
    using praxicloud.eventprocessors_legacy.kubernetes;
    using System;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// Entry point to the application
    /// </summary>
    class Program
    {
        #region Entry Point
        /// <summary>
        /// Entry point method
        /// </summary>
        static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }
        #endregion
        #region Methods
        /// <summary>
        /// Async breakout for console entry point
        /// </summary>
        private static async Task MainAsync()
        {
            var containerName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ProcessorName")) ? "LegacyProcessor" : Environment.GetEnvironmentVariable("ProcessorName");

            var baseLoggingLevel = ProcessorUtilities.GetDefaultLogLevel();
            var diagnosticDetails = new DiagnosticsConfiguration
            {
                LoggerFactory = ProcessorUtilities.GetLoggerFactory(baseLoggingLevel, ProcessorUtilities.GetLoggerSourceLevels(containerName, baseLoggingLevel)),
                MetricFactory = ProcessorUtilities.GetMetricFactory()
            };

            var container = new DemoContainer(containerName, diagnosticDetails, ProcessorUtilities.GetProbeConfiguration(), ProcessorUtilities.GetStartPosition());
            container.StartContainer();
            await container.Task.ConfigureAwait(false);
        }
        #endregion
    }
}
