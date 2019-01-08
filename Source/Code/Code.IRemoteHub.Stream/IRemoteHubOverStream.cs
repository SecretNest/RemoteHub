using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    public interface IRemoteHubOverStream : IRemoteHub
    {
        /// <summary>
        /// Stops operations on the underlying stream of the adapter used in this instance.
        /// </summary>
        /// <param name="forceClosing">How to perform closing on input stream.</param>
        /// <param name="isReadingStreamClosed">Whether the input stream is closed.</param>
        void Stop(RemoteHubStreamAdapterForceClosingMode forceClosing, out bool isInputStreamClosed);

        /// <summary>
        /// Changes the streams of the adapter used in this instance.
        /// </summary>
        /// <param name="newInputStream">Stream for reading.</param>
        /// <param name="newOuputStream">Stream for writing.</param>
        /// <remarks>When calling this method, the state of the instance should be stopped. Or, an <see cref="InvalidOperationException"/> will be thrown.</remarks>
        void ChangeStream(Stream newInputStream, Stream newOuputStream);
    }
}
