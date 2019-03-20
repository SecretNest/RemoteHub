
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        Dictionary<Guid, ClientEntity> clients = new Dictionary<Guid, ClientEntity>();

        public void ClearVirtualHosts(Guid clientId)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var record))
                {
                    record.ClearVirtualHosts(out var affectedVirtualHosts);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }

        public void Remove(Guid clientId)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var record))
                {
                    clients.Remove(clientId);
                    RefreshVirtualHost(record.VirtualHosts.Keys);
                }
            }
        }

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId)
        {
            lock (virtuals)
            {
                if (virtuals.TryGetValue(virtualHostId, out var percentageDistributer))
                {
                    clientId = percentageDistributer.GetOne();
                    return true;
                }
                else
                {
                    clientId = Guid.Empty;
                    return false;
                }
            }
        }


    }
}
