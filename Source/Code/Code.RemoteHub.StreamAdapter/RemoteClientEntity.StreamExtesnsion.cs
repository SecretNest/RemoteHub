using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class RemoteClientEntity
    {
        public IReadOnlyDictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid settingId, BinaryReader reader, out List<Guid> affectedVirtualHosts)
        {
            var count = (int)reader.ReadUInt16();
            if (count == 0)
                throw new InvalidDataException();

            affectedVirtualHosts = new List<Guid>();

            lock (virtualHostLock)
            {
                var newHosts = new Dictionary<Guid, VirtualHostSetting>();
                for (int index = 0; index < count; index++)
                {
                    var virtualHostId = reader.ReadGuid();
                    var priority = reader.ReadInt32();
                    var weight = reader.ReadInt32();
                    VirtualHostSetting virtualHost = new VirtualHostSetting(priority, weight);
                    newHosts.Add(virtualHostId, virtualHost);

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
                return newHosts;
            }
        }

        public static void SkipVirtualHostsData(BinaryReader reader)
        {
            var count = (int)reader.ReadUInt16();
            if (count == 0)
                throw new InvalidDataException();

            //Guid: 16 bytes
            //Int32: 4 bytes
            //Int32: 4 bytes
            // = Total: 24 bytes
            count *= 3;

            for (int index = 0; index < count; index++)
            {
                reader.ReadInt64();
            }
        }
    }
}
