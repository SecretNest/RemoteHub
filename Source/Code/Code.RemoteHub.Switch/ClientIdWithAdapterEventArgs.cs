using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    /// <summary>
    /// Represents an argument contains client id and the related RemoteHub Adapter instance.
    /// </summary>
    public class ClientIdWithAdapterEventArgs : ClientIdEventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <inheritdoc/>
        public IRemoteHubAdapter<byte[]> RemoteHubAdapter { get; }

        /// <summary>
        /// Initializes an instance of ClientIdWithAdapterEventArgs.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="remoteHubAdapter">Related RemoteHub Adapter instance.</param>
        public ClientIdWithAdapterEventArgs(Guid clientId, IRemoteHubAdapter<byte[]> remoteHubAdapter) : base(clientId)
        {
            RemoteHubAdapter = remoteHubAdapter;
        }
    }
}
