using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        public void AddOrUpdate(Guid clientId)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var entity))
                {
                    if (entity.VirtualHostSettingId != Guid.Empty)
                    {
                        entity.ClearVirtualHosts(out var affectedVirtualHosts);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                }
                else
                {
                    entity = new ClientEntity();
                    clients.Add(clientId, entity);
                }
            }
        }

        public void AddOrUpdate(Guid localClientId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings)
        {
            lock (clients)
            {
                if (clients.TryGetValue(localClientId, out var entity))
                {
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(virtualHostSettings);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                else
                {
                    entity = new ClientEntity();
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(virtualHostSettings);
                    clients.Add(localClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }
    }
}
