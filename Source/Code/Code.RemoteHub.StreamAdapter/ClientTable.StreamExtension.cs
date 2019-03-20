using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        public IEnumerable<Guid> GetAllRemoteClientsId()
        {
            lock (clients)
            {
                foreach(var client in clients)
                {
                    if (!client.Value.IsLocal)
                        yield return client.Key;
                }
            }
        }

        public void AddOrUpdate(Guid clientId, bool isLocal)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var entity))
                {
                    entity.IsLocal = isLocal;
                    if (entity.VirtualHostSettingId != Guid.Empty)
                    {
                        entity.ClearVirtualHosts(out var affectedVirtualHosts);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                }
                else
                {
                    entity = new ClientEntity(isLocal);
                    clients.Add(clientId, entity);
                }
            }
        }

        //for adding or updating remote clients
        public ClientEntity AddOrUpdate(Guid remoteClientId, BinaryReader inputStreamReader)
        {
            var settingId = inputStreamReader.ReadGuid();
            lock (clients)
            {
                if (clients.TryGetValue(remoteClientId, out var entity))
                {
                    entity.IsLocal = false;
                    if (entity.VirtualHostSettingId != settingId)
                    {
                        var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                    else
                    {
                        ClientEntity.SkipVirtualHostsData(inputStreamReader);
                    }
                }
                else
                {
                    entity = new ClientEntity(false);
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                    clients.Add(remoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                return entity;
            }
        }

        //for adding or updating local clients
        public void AddOrUpdate(Guid localClientId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings)
        {
            lock (clients)
            {
                if (clients.TryGetValue(localClientId, out var entity))
                {
                    entity.IsLocal = true;
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(virtualHostSettings);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                else
                {
                    entity = new ClientEntity(true);
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(virtualHostSettings);
                    clients.Add(localClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }
    }
}
