using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Remote Hub from SecretNest.info
/// </summary>
namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Handles the host state and message transportation.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) is acceptable.</typeparam>
    public class RemoteHub<T> : IDisposable
    {
        RedisClient<T> redisClient;

        /// <summary>
        /// Initiliazes an instance of RemoteHub.
        /// </summary>
        /// <param name="clientId">Client id of the local RemoteHub.</param>
        /// <param name="redisConfiguration">The string configuration to use for Redis multiplexer.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="mainChannelName">Main channel name. Default value is "RemoteHub".</param>
        /// <param name="hostKeyPrefix">Prefix in naming of the host key. Default value is "RemoteHub_". Cannot contains semicolons.</param>
        /// <param name="privateChannelNamePrefix">Prefix in naming of the private channel. Default value is "RemoteHubPrivate_".</param>
        /// <param name="redisDb">The id to get a database for. Used in getting redis database. Default value is 0.</param>
        public RemoteHub(Guid clientId, string redisConfiguration,
            OnMessageReceivedCallback<T> onMessageReceivedCallback,
            string mainChannelName = "RemoteHub", string hostKeyPrefix = "RemoteHub_",
            string privateChannelNamePrefix = "RemoteHubPrivate_", int redisDb = 0)
        {
            if (hostKeyPrefix.Contains(";"))
            {
                throw new ArgumentException("Semicolons cannot be used in " + nameof(hostKeyPrefix) + ".", nameof(hostKeyPrefix));
            }
            var type = typeof(T);
            if (type == typeof(string))
            {
                RedisClient<string> client = new RedisClientOfString(clientId, redisConfiguration, mainChannelName, hostKeyPrefix, privateChannelNamePrefix, redisDb);
                redisClient = __refvalue(__makeref(client), RedisClient<T>);
            }
            else if (type == typeof(byte[]))
            {
                RedisClient<byte[]> client = new RedisClientOfBinary(clientId, redisConfiguration, mainChannelName, hostKeyPrefix, privateChannelNamePrefix, redisDb);
                redisClient = __refvalue(__makeref(client), RedisClient<T>);
            }
            else
            {
                throw new NotSupportedException("Only string and byte array is supported.");
            }
            redisClient.OnMessageReceivedCallback = onMessageReceivedCallback;
            redisClient.RedisServerConnectionErrorOccurred += RedisClient_RedisServerConnectionErrorOccurred;
        }

        private void RedisClient_RedisServerConnectionErrorOccurred(object sender, EventArgs e)
        {
            RedisServerConnectionErrorOccurred?.Invoke(this, e);
            redisClient.RestartConnection(true);
        }

        /// <summary>
        /// Occurs while RedisConnectionException is thrown in main channel operating. Will not be raised for private channel operating.
        /// </summary>
        public event EventHandler RedisServerConnectionErrorOccurred;

        /// <summary>
        /// Gets the client id.
        /// </summary>
        public Guid ClientId => redisClient.ClientId;

        /// <summary>
        /// Restarts connection to Redis server.
        /// </summary>
        /// <param name="keepConnectionState">Start main channel processing if it's started.</param>
        public void RestartConnection(bool keepConnectionState = false)
        {
            redisClient.RestartConnection(keepConnectionState);
        }

        /// <summary>
        /// Gets or sets the TimeToLive setting of host.
        /// </summary>
        public TimeSpan HostTimeToLive
        {
            get
            {
                return redisClient.HostTimeToLive;
            }
            set
            {
                redisClient.HostTimeToLive = value;
            }
        }

        /// <summary>
        /// Applies the virtual hosts setting for current client.
        /// </summary>
        /// <param name="settings">Virtual host settings. Key is virtual host id; Value is setting related to the virtual host specified.</param>
        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings) => redisClient.ApplyVirtualHosts(settings);

        /// <summary>
        /// Tries to resolve virtual host by id to host id.
        /// </summary>
        /// <param name="virtualHostId">Virtual host id.</param>
        /// <param name="hostId">Host id.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId) => redisClient.TryResolveVirtualHost(virtualHostId, out hostId);

        /// <summary>
        /// Tries to resolve host id to private channel.
        /// </summary>
        /// <param name="hostId">Host id.</param>
        /// <param name="channel">Private channel for Redis.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        public bool TryResolve(Guid hostId, out RedisChannel channel) => redisClient.TryResolve(hostId, out channel);

        /// <summary>
        /// Sends a message to the target host specified by id.
        /// </summary>
        /// <param name="targetHostId">Target host id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>Whether the resolving from target host id is succeeded or not.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public bool SendMessage(Guid targetHostId, T message) => redisClient.SendMessage(targetHostId, message);

        /// <summary>
        /// Creates a task that sends a message to the target host specified by id.
        /// </summary>
        /// <param name="targetHostId"></param>
        /// <param name="message"></param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public async Task<bool> SendMessageAsync(Guid targetHostId, T message) => await redisClient.SendMessageAsync(targetHostId, message);

        /// <summary>
        /// Sends a message to the target host specified by private channel name.
        /// </summary>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public void SendMessage(string targetChannel, T message) => redisClient.SendMessage(targetChannel, message);

        /// <summary>
        /// Creates a task that sends a message to the target host specified by private channel name.
        /// </summary>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public async Task SendMessageAsync(string targetChannel, T message) => await redisClient.SendMessageAsync(targetChannel, message);

        /// <summary>
        /// Sends a message to the private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public void SendMessage(RedisChannel channel, T message) => redisClient.SendMessage(channel, message);

        /// <summary>
        /// Creates a task that sends a message to the private channel specified.
        /// </summary>
        /// <param name="channel">Private channel of Redis.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message.</remarks>
        public async Task SendMessageAsync(RedisChannel channel, T message) => await redisClient.SendMessageAsync(channel, message);

        /// <summary>
        /// Gets or sets the callback to be used for dealing received private message.
        /// </summary>
        public OnMessageReceivedCallback<T> OnMessageReceivedCallback
        {
            get
            {
                return redisClient.OnMessageReceivedCallback;
            }
            set
            {
                redisClient.OnMessageReceivedCallback = value;
            }
        }

        /// <summary>
        /// Starts main channel processing, including keeping server status updated and alive, syncing virtual host settings, etc.
        /// </summary>
        public void Start() => redisClient.Start();

        /// <summary>
        /// Stops main channel processing. Private channel sending and receiving will not be affected but the resolving functions cannot be executed.
        /// </summary>
        public void Shutdown() => redisClient.Shutdown();


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    redisClient.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RemoteHub() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
