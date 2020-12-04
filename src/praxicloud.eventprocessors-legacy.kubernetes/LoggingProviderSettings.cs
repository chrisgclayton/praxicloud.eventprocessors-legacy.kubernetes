// Copyright (c) Christopher Clayton. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace praxicloud.eventprocessors_legacy.kubernetes
{
    /// <summary>
    /// The provider settings for logging, used to format JSON appropriately
    /// </summary>
    internal class LoggingProviderSettings
    {
        /// <summary>
        /// The dictionary of source levels
        /// </summary>
        public Dictionary<string, string> LogLevel { get; set; }
    }
}
