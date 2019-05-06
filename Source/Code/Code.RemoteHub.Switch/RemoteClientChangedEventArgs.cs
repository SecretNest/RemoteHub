using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents an argument for <see cref="RemoteHubSwitch.RemoteClientAdded"/> and <see cref="RemoteHubSwitch.RemoteClientRemoved"/>.
    /// </summary>
    public class RemoteClientChangedEventArgs : ClientIdEventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <inheritdoc/>
        public IRemoteHubAdapter<byte[]> Adapter { get; }

        internal RemoteClientChangedEventArgs(Guid clientId, IRemoteHubAdapter<byte[]> adapter) : base(clientId)
        {
            Adapter = adapter;
        }
    }
}
