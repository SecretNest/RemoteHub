
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class HostTable
    {
        Dictionary<Guid, HostEntity> hosts = new Dictionary<Guid, HostEntity>();

        public void ClearVirtualHosts(Guid hostId)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var record))
                {
                    record.ClearVirtualHosts(out var affectedVirtualHosts);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }

        public void Remove(Guid hostId)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var record))
                {
                    hosts.Remove(hostId);
                    RefreshVirtualHost(record.VirtualHosts.Keys);
                }
            }
        }

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId)
        {
            lock (virtuals)
            {
                if (virtuals.TryGetValue(virtualHostId, out var percentageDistributer))
                {
                    hostId = percentageDistributer.GetOne();
                    return true;
                }
                else
                {
                    hostId = Guid.Empty;
                    return false;
                }
            }
        }

        List<KeyValuePair<Guid, HostEntity>> GetAllHosts()
        {
            List<KeyValuePair<Guid, HostEntity>> result = new List<KeyValuePair<Guid, HostEntity>>();
            List<Guid> hostToRemove = new List<Guid>();
            HashSet<Guid> virtualToRefresh = new HashSet<Guid>();
            lock (hosts)
            {
                foreach(var item in hosts)
                {
                    if (item.Value.IsTimeValid)
                    {
                        result.Add(item);
                    }
                    else
                    {
                        foreach (var key in item.Value.VirtualHosts.Keys)
                            virtualToRefresh.Add(key);
                        hostToRemove.Add(item.Key);
                    }
                }
                foreach(var key in hostToRemove)
                {
                    hosts.Remove(key);
                }
                if (virtualToRefresh.Count > 0)
                    RefreshVirtualHost(virtualToRefresh, result);
            }
            return result;
        }
    }
}
