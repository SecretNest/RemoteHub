using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    /// <summary>
    /// Represents an argument contains the related RemoteHub Adapter instance.
    /// </summary>
    public class AdapterEventArgs : EventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <inheritdoc/>
        public IRemoteHubAdapter<byte[]> RemoteHubAdapter { get; }

        /// <summary>
        /// Initializes an instance of AdapterEventArgs.
        /// </summary>
        /// <param name="remoteHubAdapter">Related RemoteHub Adapter instance.</param>
        public AdapterEventArgs(IRemoteHubAdapter<byte[]> remoteHubAdapter)
        {
            RemoteHubAdapter = remoteHubAdapter;
        }
    }
}
