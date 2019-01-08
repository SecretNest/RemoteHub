using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic <see cref="IRemoteHubRedisAdapter{T}"/>.
    /// </summary>
    public interface IRemoteHubRedisAdapter : IRemoteHubAdapter
    {
        /// <summary>
        /// Tries to resolve remote client id to private channel.
        /// </summary>
        /// <param name="remoteClientId">Remote client id.</param>
        /// <param name="channel">Private channel used as Redis target.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        bool TryResolve(Guid remoteClientId, out RedisChannel channel);
    }

    /// <summary>
    /// Represents the methods of RemoteHubAdapter, which converts RemoteHub commands and events to Redis database.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public interface IRemoteHubRedisAdapter<T> : IRemoteHubRedisAdapter, IRemoteHubAdapter<T>
    {
        /// <summary>
        /// Sends private message to private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        Task SendPrivateMessageAsync(RedisChannel channel, T message);

        /// <summary>
        /// Sends private message to private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        void SendPrivateMessage(RedisChannel channel, T message);
    }
}
