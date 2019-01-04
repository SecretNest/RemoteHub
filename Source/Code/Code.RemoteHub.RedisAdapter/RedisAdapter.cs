using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic <see cref="RedisAdapter{T}"/>, which converts RemoteHub commands and events to Redis database.
    /// </summary>
    public abstract class RedisAdapter : IDisposable, IRemoteHubRedisAdapter
    {
        ConnectionMultiplexer redisConnection;
        RedisChannel mainChannel;
        IDatabase redisDatabase;
        ISubscriber publisher, subscriber;
        Dictionary<Guid, ClientEntity> clients = new Dictionary<Guid, ClientEntity>();
        ConcurrentDictionary<RedisChannel, Guid> targets = new ConcurrentDictionary<RedisChannel, Guid>();
        readonly string redisConfiguration, mainChannelName, privateChannelNamePrefix;
        readonly int redisDb, clientTimeToLive, clientRefreshingInterval;
        HostTable hostTable;
        bool needRefreshFull = false;
        CancellationTokenSource updatingRedisCancellation, updatingRedisWaitingCancellation;
        Task updatingRedis;
        ManualResetEventSlim startingLock = new ManualResetEventSlim();
        AutoResetEvent clientsChangingLock = new AutoResetEvent(true); //also used in refresh sending

        /// <inheritdoc/>
        public event EventHandler<RedisExceptionEventArgs> RedisServerConnectionErrorOccurred;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                StopConnection();

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RedisAdapter() {
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

        /// <summary>
        /// Initializes an instance of RedisAdapter.
        /// </summary>
        /// <param name="redisConfiguration">The string configuration to use for Redis multiplexer.</param>
        /// <param name="mainChannelName">Main channel name.</param>
        /// <param name="privateChannelNamePrefix">Prefix in naming of the private channel.</param>
        /// <param name="redisDb">The id to get a database for. Used in getting Redis database.</param>
        /// <param name="clientTimeToLive">Time to live (TTL) value of the host in seconds. Any records of hosts expired will be removed.</param>
        /// <param name="clientRefreshingInterval">Interval between refresh command sending operations in seconds.</param>
        protected RedisAdapter(string redisConfiguration, string mainChannelName, string privateChannelNamePrefix, int redisDb, int clientTimeToLive, int clientRefreshingInterval)
        {
            this.redisConfiguration = redisConfiguration;
            this.mainChannelName = mainChannelName;
            this.privateChannelNamePrefix = privateChannelNamePrefix;
            this.redisDb = redisDb;
            mainChannel = new RedisChannel(mainChannelName, RedisChannel.PatternMode.Literal);
            this.clientTimeToLive = clientTimeToLive;
            this.clientRefreshingInterval = clientRefreshingInterval;
            hostTable = new HostTable(privateChannelNamePrefix);
        }

        #region Start Stop
        void StartConnection()
        {
            lock (startingLock)
            {
                if (updatingRedis != null) return;

                redisConnection = ConnectionMultiplexer.Connect(redisConfiguration);
                redisDatabase = redisConnection.GetDatabase(redisDb);
                publisher = redisDatabase.Multiplexer.GetSubscriber();
                subscriber = redisDatabase.Multiplexer.GetSubscriber();
                subscriber.Subscribe(mainChannel, OnMainChannelReceived);

                foreach (var clientId in clients.Keys)
                {
                    RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + clientId.ToString("N"), RedisChannel.PatternMode.Literal);
                    if (targets.TryAdd(redisChannel, clientId))
                        subscriber.Subscribe(redisChannel, OnPrivateChannelReceived);
                }

                updatingRedisCancellation = new CancellationTokenSource();
                updatingRedisWaitingCancellation = new CancellationTokenSource();

                startingLock.Reset();

                updatingRedis = UpdateRedisAsync();
                startingLock.Wait();
            }
        }

        void StopConnection()
        {
            lock (startingLock)
            {
                if (updatingRedis == null) return;

                updatingRedisCancellation.Cancel();
                updatingRedis.Wait();
                updatingRedis = null;
                updatingRedisCancellation = null;
                updatingRedisWaitingCancellation = null;

                RemoveAllClientsAsync().Wait();

                subscriber.UnsubscribeAll();

                redisConnection.Close();
                redisConnection.Dispose();
                redisDatabase = null;
                redisConnection = null;

                hostTable = new HostTable(privateChannelNamePrefix);
                clients = new Dictionary<Guid, ClientEntity>();
                targets = new ConcurrentDictionary<RedisChannel, Guid>();
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            StartConnection();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopConnection();
        }

        #endregion

        #region Main Channel Operating
        void OnMainChannelReceived(RedisChannel channel, RedisValue value)
        {
            var texts = ((string)value).Split(':');
            if (texts[0] == "v1")
            {
                if (texts[1] == "Refresh")
                {
                    var clientId = Guid.Parse(texts[2]);
                    var seconds = int.Parse(texts[3]);
                    hostTable.AddOrRefresh(clientId, seconds, out var currentVirtualHostSettingId);
                    if (texts[4] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[4]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            MainChannelPublishing("v1:NeedRefreshFull:" + texts[2]);
                        }
                    }
                    else
                    {
                        if (currentVirtualHostSettingId != Guid.Empty)
                            hostTable.ClearVirtualHosts(clientId);
                    }
                }
                else if (texts[1] == "RefreshFull")
                {
                    var clientId = Guid.Parse(texts[2]);
                    var seconds = int.Parse(texts[3]);
                    hostTable.AddOrRefresh(clientId, seconds, out var currentVirtualHostSettingId);
                    if (texts[4] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[4]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            hostTable.ApplyVirtualHosts(clientId, virtualHostId, texts[5]);
                        }
                    }
                    else
                    {
                        if (currentVirtualHostSettingId != Guid.Empty)
                            hostTable.ClearVirtualHosts(clientId);
                    }
                }
                else if (texts[1] == "Hello")
                {
                    needRefreshFull = true;
                    var old = updatingRedisWaitingCancellation;
                    updatingRedisWaitingCancellation = new CancellationTokenSource();
                    old.Cancel();
                }
                else if (texts[1] == "NeedRefreshFull")
                {
                    var clientId = Guid.Parse(texts[2]);
                    try
                    {
                        clientsChangingLock.WaitOne();
                        if (clients.TryGetValue(clientId, out var client))
                        {
                            MainChannelPublishing(client.CommandTextRefreshFull);
                        }
                    }
                    finally
                    {
                        clientsChangingLock.Set();
                    }
                }
                else if (texts[1] == "Shutdown")
                {
                    var clientId = Guid.Parse(texts[2]);
                    hostTable.Remove(clientId);
                }
            }
        }

        const string messageTextHello = "v1:Hello";
        TimeSpan smallTimeFix = new TimeSpan(0, 0, 0, 0, 200); //help to make sure at least twice refreshes before record expired.
        async Task UpdateRedisAsync()
        {
            //start
            var updatingToken = updatingRedisCancellation.Token;
            await MainChannelPublishingAsync(messageTextHello, updatingToken);

            //started
            startingLock.Set();

            var waitingToken = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token).Token;

            //keep
            var nextRefresh = DateTime.Now.AddSeconds(clientRefreshingInterval);
            while (!updatingToken.IsCancellationRequested)
            {
                try
                {
                    clientsChangingLock.WaitOne();
                    if (needRefreshFull)
                    {
                        needRefreshFull = false;
                        foreach (var client in clients.Values)
                        {
                            await MainChannelPublishingAsync(client.CommandTextRefreshFull, updatingToken);
                        }
                    }
                    else
                    {
                        foreach (var client in clients.Values)
                        {
                            await MainChannelPublishingAsync(client.CommandTextRefresh, updatingToken);
                        }
                    }
                }
                finally
                {
                    clientsChangingLock.Set();
                }

                try
                {
                    await Task.Delay(nextRefresh - DateTime.Now - smallTimeFix, waitingToken);
                }
                catch (TaskCanceledException)
                {
                    if (!updatingToken.IsCancellationRequested)
                    {
                        nextRefresh = DateTime.Now;
                        waitingToken = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token).Token;
                    }
                }
            }
        }

        void MainChannelPublishing(string text)
        {
            int retried = 0;
            try
            {
                publisher.Publish(mainChannel, text);
            }
            catch (RedisTimeoutException ex)
            {
                if (retried > 3)
                {
                    RedisServerConnectionErrorOccurred?.BeginInvoke(this, new RedisExceptionEventArgs(ex, false), null, null);
                }
                else
                {
                    retried++;
                }
            }
            catch (Exception ex) //RedisConnectionException
            {
                RedisServerConnectionErrorOccurred?.BeginInvoke(this, new RedisExceptionEventArgs(ex, true), null, null);
            }
        }

        async Task MainChannelPublishingAsync(string text, CancellationToken cancellationToken)
        {
            int retried = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await publisher.PublishAsync(mainChannel, text);
                    return;
                }
                catch (RedisTimeoutException ex)
                {
                    if (retried > 3)
                    {
                        RedisServerConnectionErrorOccurred?.BeginInvoke(this, new RedisExceptionEventArgs(ex, false), null, null);
                        break;
                    }
                    else
                    {
                        retried++;
                    }
                }
                catch (Exception ex) //RedisConnectionException
                {
                    RedisServerConnectionErrorOccurred?.BeginInvoke(this, new RedisExceptionEventArgs(ex, true), null, null);
                    break;
                }
            }
        }
        #endregion

        #region Private Channel Operating
        protected abstract void OnPrivateMessageReceived(Guid clientId, RedisValue value);

        void OnPrivateChannelReceived(RedisChannel channel, RedisValue value)
        {
            if (targets.TryGetValue(channel, out var clientId))
            {
                OnPrivateMessageReceived(clientId, value);
            }
        }

        protected async Task SendPrivateMessageAsync(RedisChannel channel, RedisValue value)
        {
            await publisher.PublishAsync(channel, value);
        }

        protected async Task SendPrivateMessageAsync(Guid targetClientId, RedisValue value)
        {
            RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + targetClientId.ToString("N"), RedisChannel.PatternMode.Literal);
            await SendPrivateMessageAsync(redisChannel, value);
        }

        protected void SendPrivateMessage(RedisChannel channel, RedisValue value)
        {
            publisher.Publish(channel, value);
        }

        protected void SendPrivateMessage(Guid targetClientId, RedisValue value)
        {
            RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + targetClientId.ToString("N"), RedisChannel.PatternMode.Literal);
            SendPrivateMessage(redisChannel, value);
        }
        #endregion

        #region Client Id
        /// <inheritdoc/>
        public async Task AddClientAsync(params Guid[] clientId)
        {
            try
            {
                clientsChangingLock.WaitOne();

                var updatingToken = updatingRedisCancellation.Token;
                foreach (var id in clientId)
                {
                    if (clients.ContainsKey(id))
                        continue;

                    string idText = id.ToString("N");
                    var client = new ClientEntity(string.Format("v1:Refresh:{0}:{1}:", idText, clientTimeToLive), string.Format("v1:RefreshFull:{0}:{1}:", idText, clientTimeToLive));
                    clients[id] = client;

                    if (updatingRedis != null)
                    {
                        RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + idText, RedisChannel.PatternMode.Literal);
                        if (targets.TryAdd(redisChannel, id))
                        {
                            await subscriber.SubscribeAsync(redisChannel, OnPrivateChannelReceived);
                        }
                        await MainChannelPublishingAsync(client.CommandTextRefreshFull, updatingToken);
                    }
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public void AddClient(params Guid[] clientId)
        {
            try
            {
                clientsChangingLock.WaitOne();

                foreach (var id in clientId)
                {
                    if (clients.ContainsKey(id))
                        continue;

                    string idText = id.ToString("N");
                    var client = new ClientEntity(string.Format("v1:Refresh:{0}:{1}:", idText, clientTimeToLive), string.Format("v1:RefreshFull:{0}:{1}:", idText, clientTimeToLive));
                    clients[id] = client;

                    if (updatingRedis != null)
                    {
                        RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + idText, RedisChannel.PatternMode.Literal);
                        if (targets.TryAdd(redisChannel, id))
                        {
                            subscriber.Subscribe(redisChannel, OnPrivateChannelReceived);
                        }
                        MainChannelPublishing(client.CommandTextRefreshFull);
                    }
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        async Task RemoveOneClientAsync(Guid clientId)
        {
            if (clients.TryGetValue(clientId, out var client))
            {
                string idText = clientId.ToString("N");
                RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + idText, RedisChannel.PatternMode.Literal);

                if (updatingRedis != null)
                {
                    await MainChannelPublishingAsync(string.Format("v1:Shutdown:{0}", idText), updatingRedisCancellation.Token);
                    if (targets.TryRemove(redisChannel, out _))
                    {
                        await subscriber.UnsubscribeAsync(redisChannel, OnPrivateChannelReceived);
                    }
                }

                clients.Remove(clientId);
            }
        }

        void RemoveOneClient(Guid clientId)
        {
            if (clients.TryGetValue(clientId, out var client))
            {
                string idText = clientId.ToString("N");
                RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + idText, RedisChannel.PatternMode.Literal);

                if (updatingRedis != null)
                {
                    MainChannelPublishing(string.Format("v1:Shutdown:{0}", idText));
                    if (targets.TryRemove(redisChannel, out _))
                    {
                        subscriber.Unsubscribe(redisChannel, OnPrivateChannelReceived);
                    }
                }

                clients.Remove(clientId);
            }
        }

        /// <inheritdoc/>
        public async Task RemoveClientAsync(params Guid[] clientId)
        {
            try
            {
                clientsChangingLock.WaitOne();

                foreach (var id in clientId)
                {
                    await RemoveOneClientAsync(id);
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public void RemoveClient(params Guid[] clientId)
        {
            try
            {
                clientsChangingLock.WaitOne();

                foreach (var id in clientId)
                {
                    RemoveOneClient(id);
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public async Task RemoveAllClientsAsync()
        {
            try
            {
                clientsChangingLock.WaitOne();

                Guid[] allId = null;

                int length = clients.Count;
                allId = new Guid[length];
                if (length > 0)
                {
                    clients.Keys.CopyTo(allId, 0);
                }

                foreach (var id in allId)
                {
                    await RemoveOneClientAsync(id);
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public void RemoveAllClients()
        {
            try
            {
                clientsChangingLock.WaitOne();

                Guid[] allId = null;

                int length = clients.Count;
                allId = new Guid[length];
                if (length > 0)
                {
                    clients.Keys.CopyTo(allId, 0);
                }

                foreach (var id in allId)
                {
                    RemoveOneClient(id);
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllClients()
        {
            Guid[] result = null;
            try
            {
                clientsChangingLock.WaitOne();
                int length = clients.Count;
                result = new Guid[length];
                if (length > 0)
                {
                    clients.Keys.CopyTo(result, 0);
                }        
            }
            finally
            {
                clientsChangingLock.Set();
            }
            return result;
        }

        #endregion

        /// <inheritdoc/>
        public void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            try
            {
                clientsChangingLock.WaitOne();

                if (clients.TryGetValue(clientId, out var client))
                {
                    client.ApplyVirtualHostSetting(settings);

                    MainChannelPublishing(client.CommandTextRefreshFull);
                }
            }
            finally
            {
                clientsChangingLock.Set();
            }
        }

        /// <inheritdoc/>
        public bool TryResolve(Guid hostId, out RedisChannel channel)
        {
            return hostTable.TryGet(hostId, out channel);
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId)
        {
            return hostTable.TryResolveVirtualHost(virtualHostId, out hostId);
        }
    }

    
}
