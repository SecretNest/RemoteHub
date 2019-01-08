using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the methods of RemoteHubAdapter, which converts RemoteHub commands and events to stream.
    /// </summary>
    public interface IRemoteHubStreamAdapter : IRemoteHubAdapter
    {
        /// <summary>
        /// Stops the underlying stream operations.
        /// </summary>
        /// <param name="forceClosing">How to perform closing on stream for reading.</param>
        /// <param name="isReadingStreamClosed">Whether the stream for reading is closed.</param>
        void Stop(RemoteHubStreamAdapterForceClosingMode forceClosing, out bool isReadingStreamClosed);

        /// <summary>
        /// Changes the streams of this adapter.
        /// </summary>
        /// <param name="newInputStream">Stream for reading.</param>
        /// <param name="newOutputStream">Stream for writing.</param>
        /// <remarks>When calling this method, the state of this adapter should be stopped. Or, an <see cref="InvalidOperationException"/> will be thrown.</remarks>
        void ChangeStream(Stream newInputStream, Stream newOutputStream);
    }
}
