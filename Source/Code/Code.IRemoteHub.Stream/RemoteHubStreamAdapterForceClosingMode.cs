using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Defines how to perform closing on input stream.
    /// </summary>
    public enum RemoteHubStreamAdapterForceClosingMode
    {
        /// <summary>
        /// Do not close the input stream.
        /// </summary>
        Default,
        /// <summary>
        /// Do not close the stream. Block this calling if the input stream is blocking in reading.
        /// </summary>
        Waiting,
        /// <summary>
        /// Close the input stream if it is blocking in reading.
        /// </summary>
        CloseWhileBlocking,
        /// <summary>
        /// Always close the input stream.
        /// </summary>
        ForceClose
    }
}
