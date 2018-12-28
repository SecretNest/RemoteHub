using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Manages multiple RemoteHub with Redis support instances.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public class MultiHubWithRedis<T> : MultiHub<T>
    {


        /// <summary>
        /// Restarts specified RemoteHub connection to Redis server.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="keepConnectionState">Start main channel processing if it's started. Default value is false.</param>
        public void RestartConnection(Guid id, bool keepConnectionState = false)
        {
            GetRemoteHub<IRemoteHubOverRedis<T>>(id).RestartConnection(keepConnectionState);
        }


        /// <summary>
        /// Restarts all RemoteHub connections to Redis server.
        /// </summary>
        /// <param name="keepConnectionState">Start main channel processing if it's started. Default value is false.</param>
        public void RestartAllConnections(bool keepConnectionState = false)
        {
            foreach (var item in GetAllRemoteHubs<IRemoteHubOverRedis<T>>())
            {
                item.RestartConnection(keepConnectionState);
            }
        }

        /// <summary>
        /// Tries to resolve host id to private channel through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="hostId">Host id.</param>
        /// <param name="channel">Private channel for Redis.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        /// <remarks>This method can only be used when the RemoteHub instance specified is implemented IRemoteHubRedis, i.e the instance based on Redis operating.</remarks>
        public bool TryResolve(Guid id, Guid hostId, out RedisChannel channel)
        {
            return GetRemoteHub<IRemoteHubOverRedis<T>>(id).TryResolve(hostId, out channel);
        }

        /// <summary>
        /// Sends a message to the private channel specified through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message. This method can only be used when the RemoteHub instance specified is implemented the generic version of IRemoteHubRedis, i.e the instance based on Redis operating.</remarks>
        public void SendMessage(Guid id, RedisChannel channel, T message)
        {
            GetRemoteHub<IRemoteHubOverRedis<T>>(id).SendMessage(channel, message);
        }

        /// <summary>
        /// Creates a task that sends a message to the private channel specified through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message. This method can only be used when the RemoteHub instance specified is implemented the generic version of IRemoteHubRedis, i.e the instance based on Redis operating.</remarks>
        public async Task SendMessageAsync(Guid id, RedisChannel channel, T message)
        {
            await GetRemoteHub<IRemoteHubOverRedis<T>>(id).SendMessageAsync(channel, message);
        }
    }
}
