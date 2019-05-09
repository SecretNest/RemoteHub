using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    partial class ClientEntity
    {
        public List<Guid> ApplyVirtualHosts(Guid settingId, BinaryReader inputStreamReader) //Return affected virtual hosts
        {
            var count = (int)inputStreamReader.ReadUInt16LittleEndian();
            if (count == 0)
                throw new InvalidDataException();

            var affectedVirtualHosts = new List<Guid>();

            var newHosts = new Dictionary<Guid, VirtualHostSetting>();
            for (int index = 0; index < count; index++)
            {
                var virtualHostId = inputStreamReader.ReadGuid();
                var priority = inputStreamReader.ReadInt32LittleEndian();
                var weight = inputStreamReader.ReadInt32LittleEndian();
                VirtualHostSetting virtualHost = new VirtualHostSetting(priority, weight);
                newHosts.Add(virtualHostId, virtualHost);
            }

            lock (virtualHostLock)
            {
                VirtualHostSettingId = settingId;
                foreach (var item in newHosts)
                {
                    var virtualHostId = item.Key;
                    var virtualHost = item.Value;

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
            return affectedVirtualHosts;
        }

        public static void SkipVirtualHostsData(BinaryReader inputStreamReader)
        {
            var count = (int)inputStreamReader.ReadUInt16LittleEndian();
            if (count == 0)
                throw new InvalidDataException();
            inputStreamReader.Skip24Bytes(count);
        }
    }
}
