using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    public abstract class ConnectionErrorOccurredEventArgsBase : EventArgs
    {
        public Guid Id { get; }
        public ConnectionErrorOccurredEventArgsBase(Guid id)
        {
            Id = id;
        }
    }


    public class ConnectionErrorOccurredEventArgs<T> : ConnectionErrorOccurredEventArgsBase
    {
        public IRemoteHub<T> RemoteHub { get; }

        public ConnectionErrorOccurredEventArgs(Guid id, IRemoteHub<T> remoteHub) : base(id)
        {
            RemoteHub = remoteHub;
        }
    }
}
