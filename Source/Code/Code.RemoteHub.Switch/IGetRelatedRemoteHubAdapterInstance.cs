using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents an object which contains a property to get the related RemoteHub Adapter instance.
    /// </summary>
    public interface IGetRelatedRemoteHubAdapterInstance
    {
        /// <summary>
        /// Gets the related RemoteHub Adapter instance.
        /// </summary>
        IRemoteHubAdapter<byte[]> RemoteHubAdapter { get; }
    }
}
