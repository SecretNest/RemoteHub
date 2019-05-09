using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class ClientEntity
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


        public bool IsVirtualHostsDisabled
        {
            get
            {
                lock (virtualHostLock)
                {
                    return VirtualHosts.Count == 0;
                }
            }
        }

        public KeyValuePair<Guid, VirtualHostSetting>[] GetVirtualHosts()
        {
            lock (virtualHostLock)
            {
                return VirtualHosts.ToArray();
            }
        }
    }
}