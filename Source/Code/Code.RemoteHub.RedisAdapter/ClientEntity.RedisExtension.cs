using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    partial class ClientEntity
    {
        public DateTime Timeout { get; private set; }
        public RedisChannel Channel { get; }

        public void Refresh(int seconds)
        {
            Timeout = DateTime.Now.AddSeconds(seconds);
        }

        public bool IsTimeValid => Timeout > DateTime.Now;

        public Dictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid settingId, string value, out List<Guid> affectedVirtualHosts)
        {
            affectedVirtualHosts = new List<Guid>();
            var newHosts = new Dictionary<Guid, VirtualHostSetting>();
            var texts = value.Split(',');
            foreach (var text in texts)
            {
                var setting = text.Split('-');
                var virtualHostId = Guid.Parse(setting[0]);
                var priority = int.Parse(setting[1]);
                var weight = int.Parse(setting[2]);
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
                affectedVirtualHosts.AddRange(VirtualHosts.Keys);//deleted
                VirtualHosts = newHosts;
            }
            return newHosts;
        }


        public ClientEntity(int seconds, string channel)
        {
            Channel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
            Refresh(seconds);
        }
    }
}
