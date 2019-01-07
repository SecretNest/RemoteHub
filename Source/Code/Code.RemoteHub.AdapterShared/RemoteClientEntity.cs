using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientEntity
    {
        public Dictionary<Guid, VirtualHostSetting> VirtualHosts { get; private set; } = new Dictionary<Guid, VirtualHostSetting>();
        public Guid VirtualHostSettingId { get; private set; }
        object virtualHostLock = new object();

        public void ClearVirtualHosts(out List<Guid> affectedVirtualHosts)
        {
            lock (virtualHostLock)
            {
                VirtualHostSettingId = Guid.Empty;
                affectedVirtualHosts = new List<Guid>(VirtualHosts.Keys);
                VirtualHosts = new Dictionary<Guid, VirtualHostSetting>();
            }
        }


    }
}