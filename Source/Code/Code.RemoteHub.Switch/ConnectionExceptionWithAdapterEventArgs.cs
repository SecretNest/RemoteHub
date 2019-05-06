using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Contains the exception occurred while accessing underlying object, and the RemoteHub Adapter instance which throw the exception.
    /// </summary>
    public class ConnectionExceptionWithAdapterEventArgs : ConnectionExceptionEventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <summary>
        /// Gets the RemoteHub Adapter instance which throw the exception.
        /// </summary>
        public IRemoteHubAdapter<byte[]> Adapter { get; }

        /// <summary>
        /// Initializes an instance of ConnectionExceptionWithAdapterEventArgs.
        /// </summary>
        /// <param name="e">The argument passed by <see cref="IRemoteHubAdapter.ConnectionErrorOccurred"/>.</param>
        /// <param name="remoteHubAdapter">RemoteHub Adapter instance which throw the exception.</param>
        public ConnectionExceptionWithAdapterEventArgs(ConnectionExceptionEventArgs e, IRemoteHubAdapter<byte[]> remoteHubAdapter)
            : base(e.Exception, e.IsFatal, e.IsRetried)
        {
            Adapter = remoteHubAdapter;
        }
    }
}
