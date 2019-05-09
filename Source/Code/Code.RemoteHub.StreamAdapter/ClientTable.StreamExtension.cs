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
        //for adding or updating remote clients
        public ClientEntity AddOrUpdate(Guid remoteClientId, BinaryReader inputStreamReader)
        {
            var settingId = inputStreamReader.ReadGuid();
            lock (clients)
            {
                if (clients.TryGetValue(remoteClientId, out var entity))
                {
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
                    entity = new ClientEntity();
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                    clients.Add(remoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                return entity;
            }
        }
    }
}
