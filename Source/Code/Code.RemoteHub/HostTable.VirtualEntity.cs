using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class HostTable
    {
        Dictionary<Guid, PercentageDistributer> virtuals = new Dictionary<Guid, PercentageDistributer>();

        public void RefreshVirtualHost(IEnumerable<Guid> virtualHosts)
        {
            lock (virtuals)
            {
                lock (hosts)
                {
                    var allHosts = GetAllHosts();

                    foreach (var virtualHost in virtualHosts)
                    {
                        Dictionary<Guid, int> realHosts = null;
                        int priority = int.MinValue;

                        foreach (var host in allHosts)
                        {
                            if (host.Item2.VirtualHosts.TryGetValue(virtualHost, out var selected))
                            {
                                if (realHosts == null || selected.Priority > priority)
                                {
                                    priority = selected.Priority;
                                    realHosts = new Dictionary<Guid, int>();
                                    realHosts.Add(host.Item1, selected.Weight);
                                }
                                else if (selected.Priority == priority)
                                {
                                    realHosts.Add(host.Item1, selected.Weight);
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
