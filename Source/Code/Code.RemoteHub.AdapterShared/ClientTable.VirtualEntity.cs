using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SecretNest.RemoteHub
{
    partial class ClientTable
    {
        Dictionary<Guid, PercentageDistributer> virtuals = new Dictionary<Guid, PercentageDistributer>();

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
