using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Handles the host state and message transportation.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) are supported.</typeparam>
    public class RemoteHubOverRedis<T> : IDisposable, IRemoteHubOverRedis<T>
    {
        readonly RedisAdapter<T> redisAdapter;
        readonly Guid clientId;

        /// <summary>
        /// Initializes an instance of RemoteHubOverRedis.
        /// </summary>
        /// <param name="clientId">Client id of the local RemoteHub.</param>
        /// <param name="redisConfiguration">The string configuration to use for Redis multiplexer.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="mainChannelName">Main channel name. Default value is "RemoteHub".</param>
        /// <param name="privateChannelNamePrefix">Prefix in naming of the private channel. Default value is "RemoteHubPrivate_".</param>
        /// <param name="redisDb">The id to get a database for. Used in getting Redis database. Default value is 0.</param>
        /// <param name="clientTimeToLive">Time to live (TTL) value of the host in seconds. Any records of hosts expired will be removed. Default value is 30 seconds.</param>
        /// <param name="clientRefreshingInterval">Interval between refresh command sending operations in seconds. Default value is 15 seconds.</param>
        public RemoteHubOverRedis(Guid clientId, string redisConfiguration,
            OnMessageReceivedCallback<T> onMessageReceivedCallback,
            string mainChannelName = "RemoteHub", string privateChannelNamePrefix = "RemoteHubPrivate_", int redisDb = 0, 
            int clientTimeToLive = 30, int clientRefreshingInterval = 15)
        {
            redisAdapter = new RedisAdapter<T>(redisConfiguration, onMessageReceivedCallback, mainChannelName, privateChannelNamePrefix, redisDb, clientTimeToLive, clientRefreshingInterval);
            this.clientId = clientId;
            redisAdapter.AddClient(clientId);
        }

        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated
        {
            add
            {
                FromAdapter_RemoteClientUpdated += value;
                redisAdapter.RemoteClientUpdated += RedisAdapter_RemoteClientUpdated; ;
            }
            remove
            {
                FromAdapter_RemoteClientUpdated -= value;
                redisAdapter.RemoteClientUpdated -= RedisAdapter_RemoteClientUpdated;
            }
        }
        private void RedisAdapter_RemoteClientUpdated(object sender, ClientWithVirtualHostSettingEventArgs e)
        {
            FromAdapter_RemoteClientUpdated?.Invoke(this, e);
        }
        event EventHandler<ClientWithVirtualHostSettingEventArgs> FromAdapter_RemoteClientUpdated;


        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved
        {
            add
            {
                FromAdapter_RemoteClientRemoved += value;
                redisAdapter.RemoteClientRemoved += RedisAdapter_RemoteClientRemoved;
            }
            remove
            {
                FromAdapter_RemoteClientRemoved -= value;
                redisAdapter.RemoteClientRemoved -= RedisAdapter_RemoteClientRemoved;
            }
        }

        private void RedisAdapter_RemoteClientRemoved(object sender, ClientIdEventArgs e)
        {
            FromAdapter_RemoteClientRemoved?.Invoke(this, e);
        }
        event EventHandler<ClientIdEventArgs> FromAdapter_RemoteClientRemoved;

        /// <inheritdoc/>
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred
        {
            add
            {
                FromAdapter_ConnectionErrorOccurred += value;
                redisAdapter.ConnectionErrorOccurred += RedisAdapter_ConnectionErrorOccurred;
            }
            remove
            {
                FromAdapter_ConnectionErrorOccurred -= value;
                redisAdapter.ConnectionErrorOccurred -= RedisAdapter_ConnectionErrorOccurred;
            }
        }
        private void RedisAdapter_ConnectionErrorOccurred(object sender, ConnectionExceptionEventArgs e)
        {
            FromAdapter_ConnectionErrorOccurred?.Invoke(this, e);
        }
        event EventHandler<ConnectionExceptionEventArgs> FromAdapter_ConnectionErrorOccurred;

        /// <inheritdoc/>
        public event EventHandler Started
        {
            add
            {
                FromAdapter_Started += value;
                redisAdapter.AdapterStarted += RedisAdapter_AdapterStarted;
            }
            remove
            {
                FromAdapter_Started -= value;
                redisAdapter.AdapterStarted -= RedisAdapter_AdapterStarted;
            }
        }
        private void RedisAdapter_AdapterStarted(object sender, EventArgs e)
        {
            FromAdapter_Started?.Invoke(this, e);
        }
        event EventHandler FromAdapter_Started;

        /// <inheritdoc/>
        public event EventHandler Stopped
        {
            add
            {
                FromAdapter_Stopped += value;
                redisAdapter.AdapterStopped += RedisAdapter_AdapterStopped;
            }
            remove
            {
                FromAdapter_Stopped -= value;
                redisAdapter.AdapterStopped -= RedisAdapter_AdapterStopped;
            }
        }
        private void RedisAdapter_AdapterStopped(object sender, EventArgs e)
        {
            FromAdapter_Stopped?.Invoke(this, e);
        }
        event EventHandler FromAdapter_Stopped;

        /// <inheritdoc/>
        public Guid ClientId => clientId;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Disposes of the resources (other than memory) used by this instance.
        /// </summary>
        /// <param name="disposing">True: release both managed and unmanaged resources; False: release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    redisAdapter.Dispose();
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
        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        /// <inheritdoc/>
        public void SendMessage(RedisChannel channel, T message)
        {
            redisAdapter.SendPrivateMessage(channel, message);
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(RedisChannel channel, T message)
        {
            await redisAdapter.SendPrivateMessageAsync(channel, message);
        }

        /// <inheritdoc/>
        public bool TryResolve(Guid clientId, out RedisChannel channel)
        {
            return redisAdapter.TryResolve(clientId, out channel);
        }

        /// <summary>
        /// Sends a message to the a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message. Redis Adapter will always return true because it won't check whether the target specified by id exists or not.</remarks>
        public void SendMessage(Guid clientId, T message)
        {
            redisAdapter.SendPrivateMessage(clientId, message);
        }

        /// <summary>
        /// Creates a task that sends a message to the a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message. Redis Adapter will always return true because it won't check whether the target specified by id exists or not.</remarks>
        public Task SendMessageAsync(Guid clientId, T message)
        {
            return redisAdapter.SendPrivateMessageAsync(clientId, message);
        }

        /// <inheritdoc/>
        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            redisAdapter.ApplyVirtualHosts(clientId, settings);
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId)
        {
            return redisAdapter.TryResolveVirtualHost(virtualHostId, out clientId);
        }

        /// <inheritdoc/>
        public void Start()
        {
            redisAdapter.Start();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            redisAdapter.Stop();
        }

        /// <inheritdoc/>
        public bool TryGetVirtualHosts(Guid clientId, out KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            return redisAdapter.TryGetVirtualHosts(clientId, out settings);
        }

        /// <inheritdoc/>
        public bool IsStarted => redisAdapter.IsStarted;
    }
}
