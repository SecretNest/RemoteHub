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
        PrivateMessageCallbackHelper<T> privateMessageCallbackHelper;
        ValueConverter<T> valueConverter;

        /// <summary>
        /// Initializes an instance of RedisAdapter.
        /// </summary>
        /// <param name="redisConfiguration">The string configuration to use for Redis multiplexer.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message. Default value is null.</param>
        /// <param name="mainChannelName">Main channel name. Default value is "RemoteHub".</param>
        /// <param name="privateChannelNamePrefix">Prefix in naming of the private channel. Default value is "RemoteHubPrivate_".</param>
        /// <param name="redisDb">The id to get a database for. Used in getting Redis database. Default value is 0.</param>
        /// <param name="clientTimeToLive">Time to live (TTL) value of the host in seconds. Any records of hosts expired will be removed. Default value is 30 seconds.</param>
        /// <param name="clientRefreshingInterval">Interval between refresh command sending operations in seconds. Default value is 15 seconds.</param>
        public RedisAdapter(string redisConfiguration, OnMessageReceivedCallback<T> onMessageReceivedCallback = null,
            string mainChannelName = "RemoteHub", string privateChannelNamePrefix = "RemoteHubPrivate_", int redisDb = 0,
            int clientTimeToLive = 30, int clientRefreshingInterval = 15)
            : base(redisConfiguration, mainChannelName, privateChannelNamePrefix, redisDb, clientTimeToLive, clientRefreshingInterval)
        {
            privateMessageCallbackHelper = new PrivateMessageCallbackHelper<T>(onMessageReceivedCallback);

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
        public void AddOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.AddCallback(callback);
            }
        }

        /// <inheritdoc/>
        public void RemoveOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.RemoveCallback(callback);
            }
        }

        /// <inheritdoc/>
        public void RemoveAllOnMessageReceivedCallbacks()
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.RemoveAllCallbacks();
            }
        }

        /// <inheritdoc/>
        public async Task SendPrivateMessageAsync(RedisChannel channel, T message)
        {
            if (IsSelf(channel, out var clientId))
            {
                OnPrivateMessageReceived(clientId, message);
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
                OnPrivateMessageReceived(clientId, message);
            }
            else
            {
                SendPrivateMessage(channel, valueConverter.ConvertToMessage(message));
            }
        }

        /// <summary>
        /// Sends private message to a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks>Redis Adapter will always return true because it won't check whether the target specified by id exists or not.</remarks>
        public async Task SendPrivateMessageAsync(Guid clientId, T message)
        {
            if (IsSelf(clientId))
            {
                OnPrivateMessageReceived(clientId, message);
            }
            else
            {
                await SendPrivateMessageAsync(clientId, valueConverter.ConvertToMessage(message));
            }
        }

        /// <summary>
        /// Sends private message to a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks>Redis Adapter will always return true because it won't check whether the target specified by id exists or not.</remarks>
        public void SendPrivateMessage(Guid clientId, T message)
        {
            if (IsSelf(clientId))
            {
                OnPrivateMessageReceived(clientId, message);
            }
            else
            {
                SendPrivateMessage(clientId, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        protected override void OnPrivateMessageReceived(Guid targetClientId, RedisValue value)
        {
            OnPrivateMessageReceived(targetClientId, valueConverter.ConvertFromMessage(value));
        }

        void OnPrivateMessageReceived(Guid targetClientId, T message)
        {
            privateMessageCallbackHelper.CallAndForget(targetClientId, message);
        }
    }
}
