using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

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


    }
}
