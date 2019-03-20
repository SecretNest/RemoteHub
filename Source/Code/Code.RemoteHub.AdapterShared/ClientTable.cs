
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        Dictionary<Guid, ClientEntity> remoteClients = new Dictionary<Guid, ClientEntity>();

        public void ClearVirtualHosts(Guid remoteClientId)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var record))
                {
                    record.ClearVirtualHosts(out var affectedVirtualHosts);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }

        //public bool IsExist(Guid remoteClientId)
        //{
        //    lock(remoteClients)
        //    {
        //        return remoteClients.ContainsKey(remoteClientId);
        //    }
        //}

        public void Remove(Guid remoteClientId)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var record))
                {
                    remoteClients.Remove(remoteClientId);
                    RefreshVirtualHost(record.VirtualHosts.Keys);
                }
            }
        }

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid remoteClientId)
        {
            lock (virtuals)
            {
                if (virtuals.TryGetValue(virtualHostId, out var percentageDistributer))
                {
                    remoteClientId = percentageDistributer.GetOne();
                    return true;
                }
                else
                {
                    remoteClientId = Guid.Empty;
                    return false;
                }
            }
        }


    }
}
