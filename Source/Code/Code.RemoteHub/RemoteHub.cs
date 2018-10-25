using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    public class RemoteHub<T> : IDisposable
    {
        RedisClient<T> redisClient;

        public RemoteHub(Guid clientId, string redisConfiguration,
            OnMessageReceivedCallback<T> onMessageReceivedCallback,
            string mainChannelName = "RemoteHub", string hostKeyPrefix = "RemoteHub_",
            string privateChannelNamePrefix = "RemoteHubPrivate_", int redisDb = 0)
        {
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

        public event EventHandler RedisServerConnectionErrorOccurred;

        public Guid ClientId => redisClient.ClientId;

        public void RestartConnection(bool keepConnectionState = false)
        {
            redisClient.RestartConnection(keepConnectionState);
        }

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

        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings) => redisClient.ApplyVirtualHosts(settings);

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId) => redisClient.TryResolveVirtualHost(virtualHostId, out hostId);

        public bool TryResolve(Guid hostId, out RedisChannel channel) => redisClient.TryResolve(hostId, out channel);

        public bool SendMessage(Guid targetHostId, T message) => redisClient.SendMessage(targetHostId, message);

        public async Task<bool> SendMessageAsync(Guid targetHostId, T message) => await redisClient.SendMessageAsync(targetHostId, message);

        public void SendMessage(string targetChannel, T message) => redisClient.SendMessage(targetChannel, message);

        public async Task SendMessageAsync(string targetChannel, T message) => await redisClient.SendMessageAsync(targetChannel, message);

        public void SendMessage(RedisChannel channel, T message) => redisClient.SendMessage(channel, message);

        public async Task SendMessageAsync(RedisChannel channel, T message) => await redisClient.SendMessageAsync(channel, message);

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

        public void Start() => redisClient.Start();

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
