using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic IRemoteHubOverRedis.
    /// </summary>
    public interface IRemoteHubOverRedis : IRemoteHub
    {

        /// <summary>
        /// Restarts connection to Redis server.
        /// </summary>
        /// <param name="keepConnectionState">Start main channel processing if it's started. Default value is false.</param>
        void RestartConnection(bool keepConnectionState = false);

        /// <summary>
        /// Tries to resolve host id to private channel.
        /// </summary>
        /// <param name="hostId">Host id.</param>
        /// <param name="channel">Private channel for Redis.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        bool TryResolve(Guid hostId, out RedisChannel channel);
    }

    /// <summary>
    /// Represents the methods, properties and event of RemoteHub, based on Redis database.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public interface IRemoteHubOverRedis<T> : IRemoteHubOverRedis, IRemoteHub<T>
    {
        /// <summary>
        /// Sends a message to the private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        void SendMessage(RedisChannel channel, T message);

        /// <summary>
        /// Creates a task that sends a message to the private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        Task SendMessageAsync(RedisChannel channel, T message);
    }
}
