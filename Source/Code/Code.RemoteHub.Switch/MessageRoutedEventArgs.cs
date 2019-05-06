using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{   /// <summary>
    /// Represents an argument for <see cref="RemoteHubSwitch.MessageRouted"/>.
    /// </summary>
    public class MessageRoutedEventArgs : ClientIdEventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <summary>
        /// Gets the message.
        /// </summary>
        public byte[] Message { get; }

        /// <inheritdoc/>
        public IRemoteHubAdapter<byte[]> Adapter { get; }

        internal MessageRoutedEventArgs(Guid clientId, IRemoteHubAdapter<byte[]> adapter, byte[] message) : base(clientId)
        {
            Adapter = adapter;
            Message = message;
        }
    }
}
