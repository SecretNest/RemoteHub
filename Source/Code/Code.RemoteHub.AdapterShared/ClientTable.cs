
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        Dictionary<Guid, ClientEntity> clients = new Dictionary<Guid, ClientEntity>(); //key = client id


        public IEnumerable<Guid> GetAllRemoteClientsId()
        {
            lock (clients)
            {
                return clients.Keys.ToArray();
            }
        }

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

        public bool Remove(Guid clientId)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var record))
                {
                    clients.Remove(clientId);
                    RefreshVirtualHost(record.VirtualHosts.Keys);
                    return true;
                }
                else
                {
                    return false;
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

        public ClientEntity Get(Guid clientId)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var entity))
                {
                    return entity;
                }
                else
                {
                    return null;
                }
            }
        }

        Dictionary<Guid, PercentageDistributer> virtuals = new Dictionary<Guid, PercentageDistributer>(); //key = virtual client id

        public void RefreshVirtualHost(IEnumerable<Guid> virtualHosts) => RefreshVirtualHost(virtualHosts.ToList());

        public void RefreshVirtualHost(List<Guid> virtualHosts)
        {
            if (virtualHosts.Count == 0)
                return;

            lock (clients)
                lock (virtuals)
                {
                    if (!clients.Any())
                    {
                        virtuals.Clear();
                    }
                    else
                    {
                        foreach (var virtualHost in virtualHosts)
                        {
                            Dictionary<Guid, int> realHosts = null;
                            int priority = int.MinValue;

                            foreach (var host in clients)
                            {
                                if (host.Value.VirtualHosts.TryGetValue(virtualHost, out var selected))
                                {
                                    if (realHosts == null || selected.Priority > priority)
                                    {
                                        priority = selected.Priority;
                                        realHosts = new Dictionary<Guid, int>();
                                        realHosts.Add(host.Key, selected.Weight);
                                    }
                                    else if (selected.Priority == priority)
                                    {
                                        realHosts.Add(host.Key, selected.Weight);
                                    }
                                }
                            }

                            virtuals[virtualHost] = new PercentageDistributer(realHosts);
                        }
                    }
                }
        }
    }
}
