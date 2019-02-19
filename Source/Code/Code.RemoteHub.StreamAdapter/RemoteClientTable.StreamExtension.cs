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
                    entity = new RemoteClientEntity();
                    var affectedVirtualHosts = entity.ApplyVirtualHosts(settingId, inputStreamReader);
                    remoteClients.Add(remoteClientId, entity);
                    RefreshVirtualHost(affectedVirtualHosts);
                }
                return entity;
            }
        }

        //public Dictionary<Guid, VirtualHostSetting> AddOrUpdate(Guid remoteClientId, Guid virtualHostSettingId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings)
        //{
        //    Dictionary<Guid, VirtualHostSetting> setting;
        //    lock (remoteClients)
        //    {
        //        if (remoteClients.TryGetValue(remoteClientId, out var entity))
        //        {
        //            if (entity.VirtualHostSettingId != virtualHostSettingId)
        //            {
        //                setting = entity.ApplyVirtualHosts(virtualHostSettingId, virtualHostSettings, out var affectedVirtualHosts);
        //                RefreshVirtualHost(affectedVirtualHosts);
        //            }
        //            else
        //            {
        //                setting = null;
        //            }
        //        }
        //        else
        //        {
        //            entity = new RemoteClientEntity();
        //            setting = entity.ApplyVirtualHosts(virtualHostSettingId, virtualHostSettings, out var affectedVirtualHosts);
        //            remoteClients.Add(remoteClientId, entity);
        //            RefreshVirtualHost(affectedVirtualHosts);
        //        }
        //    }
        //    return setting;
        //}
    }
}
