using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class HostTable
    {
        public void AddOrRefresh(Guid hostId, int seconds, string channel, out Guid virtualHostSettingId)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var entity))
                {
                    entity.Refresh(seconds);
                }
                else
                {
                    entity = new HostEntity(seconds, channel);
                    hosts.Add(hostId, entity);
                }
                virtualHostSettingId = entity.VirtualHostSettingId;
            }
        }

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

        public void ApplyVirtualHosts(Guid hostId, Guid settingId, string value)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var record))
                {
                    record.ApplyVirtualHosts(settingId, value, out var affectedVirtualHosts);
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

        public bool TryGet(Guid hostId, out RedisChannel channel)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var record))
                {
                    if (record.IsTimeValid)
                    {
                        channel = record.Channel;
                        return true;
                    }
                    else
                    {
                        hosts.Remove(hostId);
                        RefreshVirtualHost(record.VirtualHosts.Keys);
                        channel = default(RedisChannel);
                        return false;
                    }
                }
                else
                {
                    channel = default(RedisChannel);
                    return false;
                }
            }
        }

        List<Tuple<Guid, HostEntity>> GetAllHosts()
        {
            List<Tuple<Guid, HostEntity>> result = new List<Tuple<Guid, HostEntity>>();
            List<Guid> hostToRemove = new List<Guid>();
            HashSet<Guid> virtualToRefresh = new HashSet<Guid>();
            lock (hosts)
            {
                foreach(var item in hosts)
                {
                    if (item.Value.IsTimeValid)
                    {
                        result.Add(new Tuple<Guid, HostEntity>(item.Key, item.Value));
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
                    RefreshVirtualHost(virtualToRefresh);
            }
            return result;
        }
    }
}
