using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Converts RemoteHub commands and events to Redis database.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) are supported.</typeparam>
    public class RedisAdapter<T> : RedisAdapter, IRemoteHubRedisAdapter<T>
    {
        OnMessageReceivedCallback<T> onMessageReceivedCallback;
        ValueConverter<T> valueConverter;

        /// <summary>
        /// Initializes an instance of RedisAdapter.
        /// </summary>
        /// <param name="redisConfiguration">The string configuration to use for Redis multiplexer.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="mainChannelName">Main channel name.</param>
        /// <param name="privateChannelNamePrefix">Prefix in naming of the private channel.</param>
        /// <param name="redisDb">The id to get a database for. Used in getting Redis database.</param>
        /// <param name="clientTimeToLive">Time to live (TTL) value of the host in seconds. Any records of hosts expired will be removed.</param>
        /// <param name="clientRefreshingInterval">Interval between refresh command sending operations in seconds.</param>
        public RedisAdapter(string redisConfiguration, OnMessageReceivedCallback<T> onMessageReceivedCallback, string mainChannelName, string privateChannelNamePrefix, int redisDb, int clientTimeToLive, int clientRefreshingInterval)
            : base(redisConfiguration, mainChannelName, privateChannelNamePrefix, redisDb, clientTimeToLive, clientRefreshingInterval)
        {
            this.onMessageReceivedCallback = onMessageReceivedCallback;

            var type = typeof(T);
            if (type == typeof(string))
            {
                ValueConverter<string> client = new ValueConverterOfString();
                valueConverter = __refvalue(__makeref(client), ValueConverter<T>);
            }
            else if (type == typeof(byte[]))
            {
                ValueConverter<byte[]> client = new ValueConverterOfByteArray();
                valueConverter = __refvalue(__makeref(client), ValueConverter<T>);
            }
            else
            {
                throw new NotSupportedException("Only string and byte array are supported.");
            }
        }

        /// <inheritdoc/>
        public async Task SendPrivateMessageAsync(RedisChannel channel, T message)
        {
            if (IsSelf(channel, out var clientId))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() => onMessageReceivedCallback(clientId, message));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else
            {
                await SendPrivateMessageAsync(channel, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        public void SendPrivateMessage(RedisChannel channel, T message)
        {
            if (IsSelf(channel, out var clientId))
            {
                Task.Run(() => onMessageReceivedCallback(clientId, message));
            }
            else
            {
                SendPrivateMessage(channel, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        public async Task SendPrivateMessageAsync(Guid remoteClientId, T message)
        {
            if (IsSelf(remoteClientId))
            {
                OnPrivateMessageReceived(remoteClientId, message);
            }
            else
            {
                await SendPrivateMessageAsync(remoteClientId, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        public void SendPrivateMessage(Guid remoteClientId, T message)
        {
            if (IsSelf(remoteClientId))
            {
                OnPrivateMessageReceived(remoteClientId, message);
            }
            else
            {
                SendPrivateMessage(remoteClientId, valueConverter.ConvertToMessage(message));
            }
        }

        protected override void OnPrivateMessageReceived(Guid targetClientId, RedisValue value)
        {
            OnPrivateMessageReceived(targetClientId, valueConverter.ConvertFromMessage(value));
        }

        void OnPrivateMessageReceived(Guid targetClientId, T message)
        {
            Task.Run(() => onMessageReceivedCallback(targetClientId, message));
        }
    }
}
