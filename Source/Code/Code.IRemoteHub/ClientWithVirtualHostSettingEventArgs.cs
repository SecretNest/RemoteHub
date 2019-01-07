using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents an argument contains client id, virtual host setting id and setting data.
    /// </summary>
    public class ClientWithVirtualHostSettingEventArgs : ClientIdEventArgs
    {
        /// <summary>
        /// Gets the virtual host setting id.
        /// </summary>
        public Guid VirtualHostSettingId { get; }
        
        /// <summary>
        /// Gets the virtual host setting
        /// </summary>
        public KeyValuePair<Guid, VirtualHostSetting>[] VirtuaHostSetting { get; }

        /// <summary>
        /// Initializes an instance of ClientWithVirtualHostSettingEventArgs.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="virtualHostSettingId">Virtual host setting id.</param>
        /// <param name="virtuaHostSetting">Virtual host setting.</param>
        public ClientWithVirtualHostSettingEventArgs(Guid clientId, Guid virtualHostSettingId, KeyValuePair<Guid, VirtualHostSetting>[] virtuaHostSetting)
            : base(clientId)
        {
            VirtualHostSettingId = virtualHostSettingId;
            VirtuaHostSetting = virtuaHostSetting;
        }
    }
}
