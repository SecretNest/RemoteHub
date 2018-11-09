using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Provides data for <see cref="MultiHubBase.ConnectionErrorOccurred"/> event.
    /// </summary>
    public abstract class ConnectionErrorOccurredEventArgsBase : EventArgs
    {
        /// <summary>
        /// Gets the id of the identification of the related RemoteHub instance used in MultiHub.
        /// </summary>
        public Guid Id { get; }

        internal ConnectionErrorOccurredEventArgsBase(Guid id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Provides data for <see cref="MultiHub{T}.ConnectionErrorOccurredGeneric"/> event.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public class ConnectionErrorOccurredEventArgs<T> : ConnectionErrorOccurredEventArgsBase
    {
        /// <summary>
        /// Gets the related RemoteHub instance.
        /// </summary>
        public IRemoteHub<T> RemoteHub { get; }

        internal ConnectionErrorOccurredEventArgs(Guid id, IRemoteHub<T> remoteHub) : base(id)
        {
            RemoteHub = remoteHub;
        }
    }
}
