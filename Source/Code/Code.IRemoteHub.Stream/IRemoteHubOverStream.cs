using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    public interface IRemoteHubOverStream : IRemoteHub
    {
        /// <summary>
        /// Changes the streams of the adapter used in this instance.
        /// </summary>
        /// <param name="newInputStream">Stream for reading.</param>
        /// <param name="newOuputStream">Stream for writing.</param>
        /// <remarks>When calling this method, the state of the instance should be stopped. Or, an <see cref="InvalidOperationException"/> will be thrown.</remarks>
        void ChangeStream(Stream newInputStream, Stream newOuputStream);
    }
}
