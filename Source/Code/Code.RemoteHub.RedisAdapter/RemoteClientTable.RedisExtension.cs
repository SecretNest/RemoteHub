using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientTable
    {
        readonly string channelPrefix;

        internal RemoteClientTable(string channelPrefix)
        {
            this.channelPrefix = channelPrefix;
        }

        public void AddOrRefresh(Guid remoteClientId, int seconds, out Guid virtualHostSettingId)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var entity))
                {
                    entity.Refresh(seconds);
                }
                else
                {
                    entity = new RemoteClientEntity(seconds, channelPrefix + remoteClientId.ToString("N"));
                    remoteClients.Add(remoteClientId, entity);
                }
                virtualHostSettingId = entity.VirtualHostSettingId;
            }
        }

        public Dictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid remoteClientId, Guid settingId, string value)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var record))
                {
                    var setting = record.ApplyVirtualHosts(settingId, value, out var affectedVirtualHosts);
                    RefreshVirtualHost(affectedVirtualHosts);
                    return setting;
                }
                else
                {
                    return null;
                }
            }
        }

        public bool TryGet(Guid remoteClientId, out RedisChannel channel, out bool isTimedOut)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var record))
                {
                    if (record.IsTimeValid)
                    {
                        channel = record.Channel;
                        isTimedOut = false;
                        return true;
                    }
                    else
                    {
                        remoteClients.Remove(remoteClientId);
                        RefreshVirtualHost(record.VirtualHosts.Keys);
                        channel = default(RedisChannel);
                        isTimedOut = true;
                        return false;
                    }
                }
                else
                {
                    channel = default(RedisChannel);
                    isTimedOut = false;
                    return false;
                }
            }
        }

        public void ClearAllExpired(out List<Guid> expired)
        {
            expired = new List<Guid>();
            HashSet<Guid> virtualToRefresh = new HashSet<Guid>();
            lock (remoteClients)
            {
                foreach (var item in remoteClients)
                {
                    if (!item.Value.IsTimeValid)
                    {
                        foreach (var key in item.Value.VirtualHosts.Keys)
                            virtualToRefresh.Add(key);
                        expired.Add(item.Key);
                    }
                }
                foreach (var key in expired)
                {
                    remoteClients.Remove(key);
                }
                if (virtualToRefresh.Count > 0)
                    RefreshVirtualHost(virtualToRefresh);
            }
        }
    }
}
