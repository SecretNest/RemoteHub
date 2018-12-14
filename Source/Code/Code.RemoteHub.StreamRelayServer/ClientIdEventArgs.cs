using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub.StreamRelay
{
    public class ClientIdEventArgs : EventArgs
    {
        public Guid ClientId { get; }

        public ClientIdEventArgs(Guid clientId) : base ()
        {
            ClientId = clientId;
        }
    }
}
