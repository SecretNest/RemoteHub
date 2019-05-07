using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        ConcurrentDictionary<Guid, ClientEntity> clients = new ConcurrentDictionary<Guid, ClientEntity>();
        ConcurrentDictionary<RedisChannel, Guid> targets = new ConcurrentDictionary<RedisChannel, Guid>(); //listening channel and it's client id
        readonly string redisConfiguration, mainChannelName, privateChannelNamePrefix;
        readonly int redisDb, clientTimeToLive, clientRefreshingInterval;
        ClientTable hostTable;
        bool needRefreshFull = false;
        CancellationTokenSource updatingRedisCancellation, updatingRedisWaitingCancellation;
        CancellationToken updatingToken;
        Task updatingRedis;
        bool sendingNormal = false;
        bool isStopping = false;
        ManualResetEventSlim startingLock = new ManualResetEventSlim();

        /// <inheritdoc/>
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred;
        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated;
        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved;
        /// <inheritdoc/>
        public event EventHandler AdapterStarted;
        /// <inheritdoc/>
        public event EventHandler AdapterStopped;

        void CallRemoteClientUpdated(Guid clientId, Guid virtualHostSettingId, KeyValuePair<Guid, VirtualHostSetting>[] virtuaHostSetting)
        {
            if (!IsSelf(clientId))
            {
                RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, virtualHostSettingId, virtuaHostSetting));
            }
        }

        void CallRemoteClientRemoved(Guid clientId)
        {
            if (!IsSelf(clientId))
            {
                RemoteClientRemoved(this, new ClientIdEventArgs(clientId));
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                StopConnection();

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

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

        #endregion IDisposable Support

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
            hostTable = new ClientTable(privateChannelNamePrefix);
        }

        RedisChannel BuildRedisChannel(Guid id)
        {
            return BuildRedisChannel(id.ToString("N"));
        }

        RedisChannel BuildRedisChannel(string idText)
        {
            RedisChannel redisChannel = new RedisChannel(privateChannelNamePrefix + idText, RedisChannel.PatternMode.Literal);
            return redisChannel;
        }

        #region Start Stop

        void StartConnection()
        {
            lock (startingLock)
            {
                if (updatingRedis != null) return;
                sendingNormal = true;
                isStopping = false;

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
                updatingToken = updatingRedisCancellation.Token;
                updatingRedisWaitingCancellation = new CancellationTokenSource();

                startingLock.Reset();

                updatingRedis = Task.Run(async () => await UpdateRedisAsync());
                startingLock.Wait();
                AdapterStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        void StopConnection()
        {
            if (isStopping) return;
            lock (startingLock)
            {
                if (updatingRedis == null) return;
                if (isStopping) return;

                isStopping = true;

                RemoveAllClients();

                updatingRedisCancellation.Cancel();
                updatingRedis.Wait();
                updatingRedis = null;
                updatingRedisCancellation.Dispose();
                updatingRedisCancellation = null;
                updatingRedisWaitingCancellation.Dispose();
                updatingRedisWaitingCancellation = null;

                subscriber.UnsubscribeAll();

                redisConnection.Close();
                redisConnection.Dispose();
                redisDatabase = null;
                redisConnection = null;

                if (RemoteClientRemoved != null)
                {
                    Guid[] remoteClientIds;
                    Guid[] localClientIds = clients.Keys.ToArray();
                    remoteClientIds = hostTable.GetAllRemoteClientsId(localClientIds).ToArray();

                    if (remoteClientIds.Length > 0)
                    {
                        Array.ForEach(remoteClientIds, i => CallRemoteClientRemoved(i));
                    }
                }

                hostTable = new ClientTable(privateChannelNamePrefix);
                clients = new ConcurrentDictionary<Guid, ClientEntity>();
                targets = new ConcurrentDictionary<RedisChannel, Guid>();
                AdapterStopped?.Invoke(this, EventArgs.Empty);
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

        /// <inheritdoc/>
        public bool IsStarted => updatingRedis != null;

        #endregion Start Stop

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
                    hostTable.AddOrRefresh(clientId, seconds, out var currentVirtualHostSettingId, out bool isNewCreated);
                    if (texts[4] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[4]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            //new created with having-virtual-host
                            MainChannelPublishing("v1:NeedRefreshFull:" + texts[2]);
                            //delay sending RemoteClientUpdated until RefreshFull received.
                        }
                    }
                    else
                    {
                        if (currentVirtualHostSettingId != Guid.Empty)
                        {
                            //From having-virtual-host state to no-virtual-host state
                            hostTable.ClearVirtualHosts(clientId);
                            if (RemoteClientUpdated != null)
                            {
                                CallRemoteClientUpdated(clientId, Guid.Empty, null);
                            }
                        }
                        else if (isNewCreated)
                        {
                            //new created with no-virtual-host
                            if (RemoteClientUpdated != null)
                            {
                                CallRemoteClientUpdated(clientId, Guid.Empty, null);
                            }
                        }
                    }
                }
                else if (texts[1] == "RefreshFull")
                {
                    var clientId = Guid.Parse(texts[2]);
                    var seconds = int.Parse(texts[3]);
                    hostTable.AddOrRefresh(clientId, seconds, out var currentVirtualHostSettingId, out bool isNewCreated);
                    if (texts[4] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[4]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            //From no-virtual-host state to having-virtual-host state, or change virtual host setting
                            var setting = hostTable.ApplyVirtualHosts(clientId, virtualHostId, texts[5]);
                            if (RemoteClientUpdated != null)
                            {
                                CallRemoteClientUpdated(clientId, virtualHostId, setting.ToArray());
                            }
                        }
                    }
                    else
                    {
                        if (currentVirtualHostSettingId != Guid.Empty)
                        {
                            hostTable.ClearVirtualHosts(clientId);
                            //From having-virtual-host state to no-virtual-host state
                            if (RemoteClientUpdated != null)
                            {
                                CallRemoteClientUpdated(clientId, Guid.Empty, null);
                            }
                        }
                        else if (isNewCreated)
                        {
                            //new created with no-virtual-host
                            if (RemoteClientUpdated != null)
                            {
                                CallRemoteClientUpdated(clientId, Guid.Empty, null);
                            }
                        }
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
                    string refreshText;
                    if (clients.TryGetValue(clientId, out var client))
                    {
                        refreshText = client.CommandTextRefreshFull;
                    }
                    else
                    {
                        refreshText = null;
                    }
                    if (refreshText != null)
                    {
                        MainChannelPublishing(refreshText);
                    }
                }
                else if (texts[1] == "Shutdown")
                {
                    var clientId = Guid.Parse(texts[2]);
                    if (hostTable.Remove(clientId) && RemoteClientRemoved != null)
                    {
                        CallRemoteClientRemoved(clientId);
                    }
                }
            }
        }

        const string messageTextHello = "v1:Hello";
        TimeSpan smallTimeFix = new TimeSpan(0, 0, 0, 0, 200); //help to make sure at least twice refreshes before record expired.
        async Task UpdateRedisAsync()
        {
            //start
            await MainChannelPublishingAsync(messageTextHello, updatingToken);  // This Hello message will also be received by the sender, which will cause the delay in the 1st round of keeping will be canceled.

            //started
            var waitingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token);
            try
            {
                var waitingToken = waitingTokenSource.Token;
                var nextRefresh = DateTime.Now.AddSeconds(clientRefreshingInterval);

                startingLock.Set();

                //keeping
                while (!updatingToken.IsCancellationRequested)
                {
                    try
                    {
                        TimeSpan delay = nextRefresh - DateTime.Now - smallTimeFix;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, waitingToken);
                        nextRefresh = nextRefresh.AddSeconds(clientRefreshingInterval);
                    }
                    catch (TaskCanceledException)
                    {
                        if (!updatingToken.IsCancellationRequested)
                        {
                            nextRefresh = DateTime.Now.AddSeconds(clientRefreshingInterval);
                            var old = waitingTokenSource;
                            waitingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token);
                            waitingToken = waitingTokenSource.Token;
                            old.Dispose();
                        }
                    }

                    hostTable.ClearAllExpired(out var expired);
                    if (RemoteClientRemoved != null)
                        foreach (var id in expired)
                        {
                            CallRemoteClientRemoved(id);
                        }

                    string[] refreshTexts;
                    if (needRefreshFull)
                    {
                        needRefreshFull = false;
                        refreshTexts = clients.Values.Select(i => i.CommandTextRefreshFull).ToArray();
                    }
                    else
                    {
                        refreshTexts = clients.Values.Select(i => i.CommandTextRefresh).ToArray();
                    }
                    if (refreshTexts.Length > 0)
                    {
                        try
                        {
                            Array.ForEach(refreshTexts, async i => await MainChannelPublishingAsync(i, updatingToken));

                        }
                        catch (TaskCanceledException) { }
                    }
                }
            }
            finally
            {
                waitingTokenSource.Dispose();
            }
        }

        void MainChannelPublishing(string text)
        {
            int retried = 0;
            while (sendingNormal)
            {
                try
                {
                    publisher.Publish(mainChannel, text);
                    return;
                }
                catch (RedisTimeoutException ex)
                {
                    if (retried > 3)
                    {
                        if (ConnectionErrorOccurred != null)
                        {
                            Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, false, true)));
                        }
                        break;
                    }
                    else
                    {
                        retried++;
                    }
                }
                catch (Exception ex) //RedisConnectionException
                {
                    sendingNormal = false;
                    if (ConnectionErrorOccurred != null)
                    {
                        Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, true, false)));
                    }
                    StopConnection();
                    break;
                }
            }
        }

        async Task MainChannelPublishingAsync(string text, CancellationToken cancellationToken)
        {
            int retried = 0;
            while (sendingNormal && !cancellationToken.IsCancellationRequested)
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
                        if (ConnectionErrorOccurred != null)
                        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, false, true)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                        break;
                    }
                    else
                    {
                        retried++;
                    }
                }
                catch (Exception ex) //RedisConnectionException
                {
                    sendingNormal = false;
                    if (ConnectionErrorOccurred != null)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, true, false)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    StopConnection();
                    break;
                }
            }
        }

        #endregion Main Channel Operating

        #region Private Channel Operating

        protected abstract void OnPrivateMessageReceived(Guid targetClientId, RedisValue value);

        void OnPrivateChannelReceived(RedisChannel channel, RedisValue value)
        {
            if (targets.TryGetValue(channel, out var clientId))
            {
                OnPrivateMessageReceived(clientId, value);
            }
        }

        protected async Task SendPrivateMessageAsync(RedisChannel channel, RedisValue value)
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            try
            {
                await publisher.PublishAsync(channel, value);
            }
            catch (RedisTimeoutException ex)
            {
                if (ConnectionErrorOccurred != null)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, false, false)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
                throw;
            }
            catch (Exception ex)
            {
                sendingNormal = false;
                if (ConnectionErrorOccurred != null)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, true, false)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
                StopConnection();
                throw;
            }
        }

        protected async Task SendPrivateMessageAsync(Guid remoteClientId, RedisValue value)
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            RedisChannel redisChannel = BuildRedisChannel(remoteClientId);
            await SendPrivateMessageAsync(redisChannel, value);
        }

        protected void SendPrivateMessage(RedisChannel channel, RedisValue value)
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            try
            {
                publisher.Publish(channel, value);
            }
            catch (RedisTimeoutException ex)
            {
                if (ConnectionErrorOccurred != null)
                {
                    Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, false, false)));
                }
                throw;
            }
            catch (Exception ex)
            {
                sendingNormal = false;
                if (ConnectionErrorOccurred != null)
                {
                    Task.Run(() => ConnectionErrorOccurred(this, new ConnectionExceptionEventArgs(ex, true, false)));
                }
                StopConnection();
                throw;
            }
        }

        protected void SendPrivateMessage(Guid remoteClientId, RedisValue value)
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            RedisChannel redisChannel = BuildRedisChannel(remoteClientId);
            SendPrivateMessage(redisChannel, value);
        }

        #endregion Private Channel Operating

        #region Client Id

        protected bool IsSelf(Guid clientId)
        {
            return clients.ContainsKey(clientId);
        }

        protected bool IsSelf(RedisChannel redisChannel, out Guid clientId)
        {
            return targets.TryGetValue(redisChannel, out clientId); //targets: contains all local listening channel and it's client id.
        }

        List<string> AddClientWithoutSendingRefresh(Guid[] clientId)
        {
            lock (startingLock)
            {
                List<string> refreshTexts = new List<string>();
                bool isStarted = IsStarted;
                List<RedisChannel> channelToBeSubscribed;
                if (isStarted)
                    channelToBeSubscribed = new List<RedisChannel>();
                else
                    channelToBeSubscribed = null;


                foreach (var id in clientId)
                {
                    string idText = id.ToString("N");
                    var client = new ClientEntity(string.Format("v1:Refresh:{0}:{1}:", idText, clientTimeToLive), string.Format("v1:RefreshFull:{0}:{1}:", idText, clientTimeToLive));
                    if (clients.TryAdd(id, client))
                    { 
                        if (isStarted)
                        {
                            RedisChannel redisChannel = BuildRedisChannel(idText);
                            if (targets.TryAdd(redisChannel, id))
                            {
                                channelToBeSubscribed.Add(redisChannel);
                            }
                            refreshTexts.Add(client.CommandTextRefreshFull);
                        }
                    }
                }
                if (isStarted)
                    channelToBeSubscribed.ForEach(i => subscriber.Subscribe(i, OnPrivateChannelReceived));
                return refreshTexts;
            }
        }

        /// <inheritdoc/>
        public async Task AddClientAsync(params Guid[] clientId)
        {
            try
            {
                var toRefresh = AddClientWithoutSendingRefresh(clientId);
                foreach(var text in toRefresh)
                {
                    await MainChannelPublishingAsync(text, updatingToken);
                }
            }
            catch (TaskCanceledException) { }
        }

        /// <inheritdoc/>
        public void AddClient(params Guid[] clientId)
        {
            AddClientWithoutSendingRefresh(clientId).ForEach(i => MainChannelPublishing(i));
        }

        async Task RemoveOneClientAsync(string idText)
        {
            //dont need to lock. no harm if removing while not started.
            bool isStarted = IsStarted;
            if (isStarted)
            {
                await MainChannelPublishingAsync(string.Format("v1:Shutdown:{0}", idText), updatingRedisCancellation.Token);
                RedisChannel redisChannel = BuildRedisChannel(idText);
                if (targets.TryRemove(redisChannel, out _))
                {
                    await subscriber.UnsubscribeAsync(redisChannel, OnPrivateChannelReceived);
                }


            }
        }

        void RemoveOneClient(string idText)
        {
            //dont need to lock. no harm if removing while not started.
            bool isStarted = IsStarted;
            if (isStarted)
            {
                MainChannelPublishing(string.Format("v1:Shutdown:{0}", idText));
                RedisChannel redisChannel = BuildRedisChannel(idText);
                if (targets.TryRemove(redisChannel, out _))
                {
                    subscriber.Unsubscribe(redisChannel, OnPrivateChannelReceived);
                }
            }
        }

        List<string> RemoveClientWithoutTargetNorRedisOperating(Guid[] clientId)
        {
            List<string> allIdText = new List<string>();
            foreach (var id in clientId)
            {
                if (clients.TryRemove(id, out _))
                {
                    allIdText.Add(id.ToString("N"));
                }
            }
            return allIdText;
        }

        /// <inheritdoc/>
        public async Task RemoveClientAsync(params Guid[] clientId)
        {
            List<string> allIdText = RemoveClientWithoutTargetNorRedisOperating(clientId);

            foreach(var idText in allIdText)
            {
                await RemoveOneClientAsync(idText);
            }
        }

        /// <inheritdoc/>
        public void RemoveClient(params Guid[] clientId)
        {
            List<string> allIdText = RemoveClientWithoutTargetNorRedisOperating(clientId);
            allIdText.ForEach(i => RemoveOneClient(i));
        }

        string[] RemoveAllClientWithoutTargetNorRedisOperating()
        {
            string[] allIdText = Array.ConvertAll(clients.Keys.ToArray(), i => i.ToString("N"));
            return allIdText;
        }

        /// <inheritdoc/>
        public async Task RemoveAllClientsAsync()
        {
            string[] allIdText = RemoveAllClientWithoutTargetNorRedisOperating();

            foreach (var idText in allIdText)
            {
                await RemoveOneClientAsync(idText);
            }
        }

        /// <inheritdoc/>
        public void RemoveAllClients()
        {
            string[] allIdText = RemoveAllClientWithoutTargetNorRedisOperating();
            foreach (var idText in allIdText)
            {
                RemoveOneClient(idText);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllClients()
        {
            Guid[] result = clients.Keys.ToArray();
            return result;
        }

        #endregion Client Id

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllRemoteClients()
        {
            Guid[] localClients = clients.Keys.ToArray();
            Guid[] result = hostTable.GetAllRemoteClientsId(localClients).ToArray();
            return result;
        }

        /// <inheritdoc/>
        public void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            string refreshText;

            if (clients.TryGetValue(clientId, out var client))
            {
                client.ApplyVirtualHostSetting(settings);
                refreshText = client.CommandTextRefreshFull;
            }
            else
            {
                refreshText = null;
            }
            if (refreshText != null)
            {
                MainChannelPublishing(refreshText);
            }
        }

        /// <inheritdoc/>
        public bool TryResolve(Guid clientId, out RedisChannel channel)
        {
            var result = hostTable.TryGet(clientId, out channel, out var isTimedOut);
            if (isTimedOut && RemoteClientRemoved != null)
            {
                CallRemoteClientRemoved(clientId);
            }
            if (!result) //try local
            {
                if (clients.ContainsKey(clientId))
                {
                    channel = BuildRedisChannel(clientId);
                    result = true;
                }
            }
            return result;
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId)
        {
            return hostTable.TryResolveVirtualHost(virtualHostId, out clientId);
        }
    }
}