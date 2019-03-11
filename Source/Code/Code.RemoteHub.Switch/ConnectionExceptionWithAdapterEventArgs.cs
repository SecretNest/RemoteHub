using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    /// <summary>
    /// Contains the exception occurred while accessing underlying object, and the RemoteHub Adapter instance which throw the exception.
    /// </summary>
    public class ConnectionExceptionWithAdapterEventArgs : ConnectionExceptionEventArgs, IGetRelatedRemoteHubAdapterInstance
    {
        /// <summary>
        /// Gets the RemoteHub Adapter instance which throw the exception.
        /// </summary>
        public IRemoteHubAdapter<byte[]> RemoteHubAdapter { get; }

        /// <summary>
        /// Initializes an instance of ConnectionExceptionWithAdapterEventArgs.
        /// </summary>
        /// <param name="exception">The exception occurred while accessing underlying object.</param>
        /// <param name="isFatal">Whether this exception is a fatal one which terminated the connection and further jobs.</param>
        /// <param name="isRetried">Whether the operation which raised this exception is retried.</param>
        /// <param name="remoteHubAdapter">RemoteHub Adapter instance which throw the exception.</param>
        public ConnectionExceptionWithAdapterEventArgs(Exception exception, bool isFatal, bool isRetried, IRemoteHubAdapter<byte[]> remoteHubAdapter)
            : base(exception, isFatal, isRetried)
        {
            RemoteHubAdapter = remoteHubAdapter;
        }
    }
}
