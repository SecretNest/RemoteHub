using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    partial class ClientEntity
    {

        public string CommandTextRefresh { get; private set; }
        public string CommandTextRefreshFull { get; private set; }

        string prefixForRefresh, prefixForRefreshFull;

        bool TimerEnabled;
        public DateTime Timeout { get; private set; }
        public RedisChannel Channel { get; }


        public ClientEntity(string prefixForRefresh, string prefixForRefreshFull) //for local clients
        {
            this.prefixForRefresh = prefixForRefresh;
            this.prefixForRefreshFull = prefixForRefreshFull;
            CommandTextRefresh = prefixForRefresh;
            CommandTextRefreshFull = prefixForRefreshFull;
            TimerEnabled = false;
        }

        public void ApplyVirtualHostSetting(params KeyValuePair<Guid, VirtualHostSetting>[] settings) //from client command. dont need to apply to VirtualHosts coz it will be done in processing the loopback message from redis.
        {
            if (settings == null || settings.Length == 0)
            {
                CommandTextRefresh = prefixForRefresh;
                CommandTextRefreshFull = prefixForRefresh;
            }
            else
            {
                string id = Guid.NewGuid().ToString("N");
                CommandTextRefresh = prefixForRefresh + id;
                CommandTextRefreshFull = prefixForRefreshFull + id + ":"
                     + string.Join(",", Array.ConvertAll(settings, i => string.Format("{0:N}-{1}-{2}", i.Key, i.Value.Priority, i.Value.Weight)));
            }
        }


        public void Refresh(int seconds)
        {
            if (TimerEnabled)
                Timeout = DateTime.Now.AddSeconds(seconds);
        }

        public bool IsTimeValid => !TimerEnabled || Timeout > DateTime.Now;

        public Dictionary<Guid, VirtualHostSetting> ApplyVirtualHosts(Guid settingId, string value, out List<Guid> affectedVirtualHosts) //from redis
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


        public ClientEntity(int seconds, string channel) //for remote clients
        {
            Channel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
            Refresh(seconds);
            TimerEnabled = true;
        }
    }
}
