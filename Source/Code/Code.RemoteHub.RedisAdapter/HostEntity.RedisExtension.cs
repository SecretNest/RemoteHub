using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    partial class HostEntity
    {
        public RedisChannel Channel { get; }

        public IReadOnlyDictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid settingId, string value, out List<Guid> affectedVirtualHosts)
        {
            affectedVirtualHosts = new List<Guid>();
            lock (virtualHostLock)
            {
                var newHosts = new Dictionary<Guid, VirtualHostSetting>();
                var texts = value.Split(',');
                VirtualHostSettingId = settingId;
                foreach (var text in texts)
                {
                    var setting = text.Split('-');
                    var virtualHostId = Guid.Parse(setting[0]);
                    var priority = int.Parse(setting[1]);
                    var weight = int.Parse(setting[2]);
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


        public HostEntity(int seconds, string channel)
        {
            Channel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
            Refresh(seconds);
        }
    }
}
