using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        readonly string channelPrefix;

        public IEnumerable<Guid> GetAllRemoteClientsId(ICollection<Guid> localClients)
        {
            lock (clients)
            {
                foreach(var id in clients.Keys)
                {
                    if (!localClients.Contains(id))
                        yield return id;
                }
            }
        }

        internal ClientTable(string channelPrefix)
        {
            this.channelPrefix = channelPrefix;
        }

        public void AddOrRefresh(Guid clientId, int seconds, out Guid virtualHostSettingId, out bool isNewCreated)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var entity))
                {
                    isNewCreated = false;
                    entity.Refresh(seconds);
                }
                else
                {
                    isNewCreated = true;
                    entity = new ClientEntity(seconds, channelPrefix + clientId.ToString("N"));
                    clients.Add(clientId, entity);
                }
                virtualHostSettingId = entity.VirtualHostSettingId;
            }
        }

        public Dictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid clientId, Guid settingId, string value)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var record))
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

        public bool TryGet(Guid clientId, out RedisChannel channel, out bool isTimedOut)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var record))
                {
                    if (record.IsTimeValid)
                    {
                        channel = record.Channel;
                        isTimedOut = false;
                        return true;
                    }
                    else
                    {
                        clients.Remove(clientId);
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
            lock (clients)
            {
                foreach (var item in clients)
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
                    clients.Remove(key);
                }
                if (virtualToRefresh.Count > 0)
                    RefreshVirtualHost(virtualToRefresh);
            }
        }
    }
}
