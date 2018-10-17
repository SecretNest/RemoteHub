using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    partial class HostTable
    {
        Dictionary<Guid, HostEntity> hosts = new Dictionary<Guid, HostEntity>();
        class HostEntity
        {
            public DateTime Timeout { get; private set; }
            public RedisChannel Channel { get; }

            public Dictionary<Guid, VirtualHostSetting> VirtualHosts { get; private set; } = new Dictionary<Guid, VirtualHostSetting>();
            public Guid VirtualHostSettingId { get; private set; }
            object virtualHostLock = new object();

            public void ClearVirtualHosts(out List<Guid> affectedVirtualHosts)
            {
                lock (virtualHostLock)
                {
                    VirtualHostSettingId = Guid.Empty;
                    affectedVirtualHosts = new List<Guid>(VirtualHosts.Keys);
                    VirtualHosts = new Dictionary<Guid, VirtualHostSetting>();
                }
            }

            public void ApplyVirtualHosts(Guid settingId, string value, out List<Guid> affectedVirtualHosts)
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
                }
            }

            public void Refresh(int seconds)
            {
                Timeout = DateTime.Now.AddSeconds(seconds);
            }

            public bool IsTimeValid => Timeout > DateTime.Now;

            public HostEntity(int seconds, string channel)
            {
                Channel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
                Refresh(seconds);
            }
        }
    }
}
