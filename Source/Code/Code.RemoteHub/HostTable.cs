using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class HostTable
    {
        Dictionary<Guid, Entity> hosts = new Dictionary<Guid, Entity>();

        class Entity
        {
            public DateTime Timeout { get; private set; }
            public RedisChannel Channel { get; }

            public void Refresh(int seconds)
            {
                Timeout = DateTime.Now.AddSeconds(seconds);
            }

            public bool IsTimeValid => Timeout > DateTime.Now;

            public Entity(int seconds, string channel)
            {
                Channel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
                Refresh(seconds);
            }
        }
        
        public void AddOrRefresh(Guid hostId, int seconds, string channel)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var entity))
                {
                    entity.Refresh(seconds);
                }
                else
                {
                    hosts.Add(hostId, new Entity(seconds, channel));
                }
            }
        }

        public void Remove(Guid hostId)
        {
            lock (hosts)
            {
                hosts.Remove(hostId);
            }
        }

        public bool TryGet(Guid hostId, out RedisChannel channel)
        {
            lock (hosts)
            {
                if (hosts.TryGetValue(hostId, out var record))
                {
                    if (record.IsTimeValid)
                    {
                        channel = record.Channel;
                        return true;
                    }
                    else
                    {
                        hosts.Remove(hostId);
                        channel = default(RedisChannel);
                        return false;
                    }
                }
                else
                {
                    channel = default(RedisChannel);
                    return false;
                }
            }
        }

        public List<Tuple<Guid, RedisChannel>> GetAllHosts()
        {
            List<Tuple<Guid, RedisChannel>> result = new List<Tuple<Guid, RedisChannel>>();
            List<Guid> toRemove = new List<Guid>();
            lock (hosts)
            {
                foreach(var item in hosts)
                {
                    if (item.Value.IsTimeValid)
                    {
                        result.Add(new Tuple<Guid, RedisChannel>(item.Key, item.Value.Channel));
                    }
                    else
                    {
                        toRemove.Add(item.Key);
                    }
                }
                foreach(var key in toRemove)
                {
                    hosts.Remove(key);
                }
            }
            return result;
        }
    }
}
