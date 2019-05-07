using StackExchange.Redis;
using System;

namespace RedisMonitoring
{
    class Program
    {
        static void Main(string[] args)
        {
            ConnectionMultiplexer redisConnection = ConnectionMultiplexer.Connect("localhost");
            var redisDatabase = redisConnection.GetDatabase(0);
            var subscriber = redisDatabase.Multiplexer.GetSubscriber();
            RedisChannel redisChannel = new RedisChannel("*", RedisChannel.PatternMode.Pattern);
            subscriber.Subscribe(redisChannel, OnMainChannelReceived);
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey(true);

            subscriber.UnsubscribeAll();
            redisConnection.Close();
            redisConnection.Dispose();
            redisDatabase = null;
            redisConnection = null;
        }

        static void OnMainChannelReceived(RedisChannel channel, RedisValue value)
        {
            Console.WriteLine("{0}: {1}", channel, value);
        }
    }
}
