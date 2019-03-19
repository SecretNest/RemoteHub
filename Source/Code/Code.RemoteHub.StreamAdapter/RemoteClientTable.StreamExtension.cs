using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientTable
    {
        public IEnumerable<Guid> GetAllRemoteClientId()
        {
            lock (remoteClients)
            {
                foreach(var client in remoteClients)
                {
                    if (!client.Value.IsLocal)
                        yield return client.Key;
                }
            }
        }

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
                    entity = new RemoteClientEntity(false);
                    remoteClients.Add(remoteClientId, entity);
                }
            }
        }

        public RemoteClientEntity AddOrUpdate(Guid remoteClientId, BinaryReader inputStreamReader)
        {
            var settingId = inputStreamReader.ReadGuid();
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(remoteClientId, out var entity))
                {
                    if (entity.VirtualHostSettingId != settingId)
                    {
                        var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                        RefreshVirtualHost(affectedVirtualHosts);
                    }
                    else
                    {
                        RemoteClientEntity.SkipVirtualHostsData(inputStreamReader);
                    }
                }
                else
                {
                    entity = new RemoteClientEntity(false);
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                    remoteClients.Add(remoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                return entity;
            }
        }

        public void AddOrUpdateLocalAsRemoteForVirtualHost(Guid fakeRemoteClientId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings)
        {
            lock (remoteClients)
            {
                if (remoteClients.TryGetValue(fakeRemoteClientId, out var entity))
                {
                    //if (entity.IsLocal)
                    //{
                    //if (entity.VirtualHostSettingId != virtualHostSettingId)
                    //{
                    var affectedVirtualHosts = entity.ApplyVirtualHostsForLocalClient(virtualHostSettings);
                    RefreshVirtualHost(affectedVirtualHosts);
                    //}
                    //}
                }
                else
                {
                    entity = new RemoteClientEntity(true);
                    var affectedVirtualHosts = entity.ApplyVirtualHostsForLocalClient(virtualHostSettings);
                    remoteClients.Add(fakeRemoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
            }
        }
    }
}
