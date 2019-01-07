using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientTable
    {
        public void AddOrUpdate(Guid remoteClientId)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var entity))
                {
                    if (entity.VirtualHostSettingId != Guid.Empty)
                    {
                        entity.ClearVirtualHosts(out var affectedVirtualHosts);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                }
                else
                {
                    entity = new RemoteClientEntity();
                    remoteClients.Add(remoteClientId, entity);
                }
            }
        }

        public void AddOrUpdate(Guid remoteClientId, BinaryReader reader)
        {
            var settingId = reader.ReadGuid();
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var entity))
                {
                    if (entity.VirtualHostSettingId != settingId)
                    {
                        entity.ApplyVirtualHosts(settingId, reader, out var affectedVirtualHosts);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                    else
                    {
                        RemoteClientEntity.SkipVirtualHostsData(reader);
                    }
                }
                else
                {
                    entity = new RemoteClientEntity();
                    entity.ApplyVirtualHosts(settingId, reader, out var affectedVirtualHosts);
                    remoteClients.Add(remoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }
    }
}
