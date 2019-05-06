using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents an argument for <see cref="RemoteHubSwitch.MessageRoutingFailed"/>.
    /// </summary>
    public class MessageRoutingFailedEventArgs : ClientIdEventArgs
    {
        /// <summary>
        /// Gets the message.
        /// </summary>
        public byte[] Message { get; }

        internal MessageRoutingFailedEventArgs(Guid clientId, byte[] message) : base(clientId)
        {
            Message = message;
        }
    }
}
