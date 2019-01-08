using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientEntity
    {
        public async Task<List<Guid>> ApplyVirtualHostsAsync(Guid settingId, Stream inputStream) //Return affected virtual hosts
        {
            var count = (int)await inputStream.ReadUInt16();
            if (count == 0)
                throw new InvalidDataException();

            var affectedVirtualHosts = new List<Guid>();

            var newHosts = new Dictionary<Guid, VirtualHostSetting>();
            for (int index = 0; index < count; index++)
            {
                var virtualHostId = await inputStream.ReadGuid();
                var priority = await inputStream.ReadInt32();
                var weight = await inputStream.ReadInt32();
                VirtualHostSetting virtualHost = new VirtualHostSetting(priority, weight);
                newHosts.Add(virtualHostId, virtualHost);
            }

            lock (virtualHostLock)
            {
                foreach(var item in newHosts)
                {
                    var virtualHostId = item.Key;
                    var virtualHost = item.Value;

                    if (VirtualHosts.TryGetValue(virtualHostId, out var oldVirtualHost))
                    {
                        if (oldVirtualHost != virtualHost)
                        {
                            affectedVirtualHosts.Add(virtualHostId);
                        }
                        VirtualHosts.Remove(virtualHostId);
                    }
                    else
                    {
                        affectedVirtualHosts.Add(virtualHostId);
                    }
                }
                affectedVirtualHosts.AddRange(VirtualHosts.Keys);
                VirtualHosts = newHosts;
            }
            return affectedVirtualHosts;
        }

        public static async Task SkipVirtualHostsDataAsync(Stream inputStream)
        {
            var count = (int)await inputStream.ReadUInt16();
            if (count == 0)
                throw new InvalidDataException();
            await inputStream.Skip24Bytes(count);
        }

        public Dictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid settingId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings, out List<Guid> affectedVirtualHosts)
        {
            affectedVirtualHosts = new List<Guid>();

            var newHosts = new Dictionary<Guid, VirtualHostSetting>();
            lock (virtualHostLock)
            {
                foreach(var virtualHostSetting in virtualHostSettings)
                {
                    var virtualHostId = virtualHostSetting.Key;
                    var virtualHost = virtualHostSetting.Value;

                    newHosts.Add(virtualHostId, virtualHost);

                    if (VirtualHosts.TryGetValue(virtualHostId, out var oldVirtualHost))
                    {
                        if (oldVirtualHost != virtualHost)
                        {
                            affectedVirtualHosts.Add(virtualHostId);//changed
                        }
                        VirtualHosts.Remove(virtualHostId);
                    }
                    else
                    {
                        affectedVirtualHosts.Add(virtualHostId);//added
                    }
                }
                affectedVirtualHosts.AddRange(VirtualHosts.Keys);//removed
                VirtualHosts = newHosts;
            }
            return newHosts;
        }
    }
}
