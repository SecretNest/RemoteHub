using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents an argument contains client id.
    /// </summary>
    public class ClientIdEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the client id.
        /// </summary>
        public Guid ClientId { get; }

        /// <summary>
        /// Initializes an instance of ClientIdEventArgs.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        public ClientIdEventArgs(Guid clientId)
        {
            ClientId = clientId;
        }
    }
}
