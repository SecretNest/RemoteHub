using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    abstract class RedisClient<T> : IDisposable
    {
        ConnectionMultiplexer redisConnection;
        RedisChannel mainChannel, privateChannel;
        IDatabase redisDatabase;
        ISubscriber publisher;
        ISubscriber subscriber;
        readonly int redisDb;
        readonly string mainChannelName, privateChannelName, redisConfiguration;
        string messageTextRefresh;
        string messageTextRefreshFull;
        readonly string messageTextShutdown;
        const string messageTextHello = "v1:Hello";
        TimeSpan hostTimeToLive = new TimeSpan(0, 0, 30);
        TimeSpan hostRefreshingTime = new TimeSpan(0, 0, 15);
        readonly string clientIdText;
        readonly Guid clientId;

        HostTable hostTable = new HostTable();

        public Guid ClientId => clientId;

        protected RedisClient(Guid clientId, string redisConfiguration, string mainChannelName, string privateChannelNamePrefix, int redisDb)
        {
            this.clientId = clientId;
            this.mainChannelName = mainChannelName;
            this.redisConfiguration = redisConfiguration;
            clientIdText = clientId.ToString("N");
            privateChannelName = privateChannelNamePrefix + clientIdText;
            messageTextShutdown = "v1:Shutdown:" + clientIdText;
            this.redisDb = redisDb;
            BuildMessageTextRefresh();
            StartConnection();
        }

        void StartConnection()
        {
            redisConnection = ConnectionMultiplexer.Connect(redisConfiguration);
            redisDatabase = redisConnection.GetDatabase(redisDb);
            mainChannel = new RedisChannel(mainChannelName, RedisChannel.PatternMode.Literal);
            privateChannel = new RedisChannel(privateChannelName, RedisChannel.PatternMode.Literal);
            subscriber = redisDatabase.Multiplexer.GetSubscriber();
            subscriber.Subscribe(mainChannel, OnMainChannelReceived);
            subscriber.Subscribe(privateChannel, OnPrivateChannelReceived);

            publisher = redisDatabase.Multiplexer.GetSubscriber();
        }

        public void RestartConnection(bool keepConnectionState)
        {
            lock (startingLock)
            {
                var connected = updatingRedis != null;
                if (connected) Shutdown();
                redisConnection.Dispose();
                StartConnection();
                if (connected && keepConnectionState) Start();
            }
        }

        public TimeSpan HostTimeToLive
        {
            get
            {
                return hostTimeToLive;
            }
            set
            {
                if (value.TotalSeconds < 5)
                    hostTimeToLive = new TimeSpan(0, 0, 5);
                else
                    hostTimeToLive = value;
                BuildMessageTextRefresh();
                hostRefreshingTime = new TimeSpan(hostTimeToLive.Ticks / 2);
            }
        }

        string currentVirtualHostSettingId;
        string currentVirtualHostSetting;
        bool needRefreshFull = false;

        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            if (settings == null || settings.Length == 0)
            {
                currentVirtualHostSettingId = Guid.Empty.ToString("N");
                currentVirtualHostSetting = currentVirtualHostSettingId;
            }
            else
            {
                currentVirtualHostSettingId = Guid.NewGuid().ToString("N");
                currentVirtualHostSetting = currentVirtualHostSettingId + ":"
                    + string.Join(",", Array.ConvertAll(settings, i => string.Format("{0:N}-{1}-{2}", i.Key, i.Value.Priority, i.Value.Weight)));
            }

            BuildMessageTextRefresh();

            needRefreshFull = true;
            if (updatingRedisWaitingCancellation != null)
            {
                var old = updatingRedisWaitingCancellation;
                updatingRedisWaitingCancellation = new CancellationTokenSource();
                old.Cancel();
            }
        }

        void BuildMessageTextRefresh()
        {
            messageTextRefreshFull = string.Format("v1:RefreshFull:{0}:{1}:{2}:{3}",
                clientIdText, (int)hostTimeToLive.TotalSeconds, privateChannelName, currentVirtualHostSetting);
            messageTextRefresh = string.Format("v1:Refresh:{0}:{1}:{2}:{3}",
                clientIdText, (int)hostTimeToLive.TotalSeconds, privateChannelName, currentVirtualHostSettingId);
        }

        void OnMainChannelReceived(RedisChannel channel, RedisValue value)
        {
            var texts = ((string)value).Split(':');
            if (texts[0] == "v1")
            {
                if (texts[1] == "Refresh")
                {
                    var clientId = Guid.Parse(texts[2]);
                    var seconds = int.Parse(texts[3]);
                    var channelName = texts[4];
                    hostTable.AddOrRefresh(clientId, seconds, channelName, out var currentVirtualHostSettingId);
                    if (texts[5] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[5]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            publisher.PublishAsync(mainChannel, "v1:NeedRefreshFull:" + texts[2]);
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
                    var channelName = texts[4];
                    hostTable.AddOrRefresh(clientId, seconds, channelName, out var currentVirtualHostSettingId);
                    if (texts[5] != "")
                    {
                        Guid virtualHostId = Guid.Parse(texts[5]);
                        if (currentVirtualHostSettingId != virtualHostId)
                        {
                            hostTable.ApplyVirtualHosts(clientId, virtualHostId, texts[6]);
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
                    if (clientIdText == texts[2])
                    {
                        needRefreshFull = true;
                        var old = updatingRedisWaitingCancellation;
                        updatingRedisWaitingCancellation = new CancellationTokenSource();
                        old.Cancel();
                    }
                }
                else if (texts[1] == "Shutdown")
                {
                    var clientId = Guid.Parse(texts[2]);
                    hostTable.Remove(clientId);
                }
            }
        }

        void OnPrivateChannelReceived(RedisChannel channel, RedisValue value)
        {
            if (onMessageReceivedCallback != null)
            {
                T message = ConvertFromRedisValue(value);
                onMessageReceivedCallback(clientId, message);
            }
        }

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId)
        {
            return hostTable.TryResolveVirtualHost(virtualHostId, out hostId);
        }

        public bool TryResolve(Guid hostId, out RedisChannel channel)
        {
            return hostTable.TryGet(hostId, out channel);
        }

        public bool SendMessage(Guid targetHostId, T message)
        {
            if (hostTable.TryGet(targetHostId, out var channel))
            {
                SendMessage(channel, message);
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> SendMessageAsync(Guid targetHostId, T message)
        {
            if (hostTable.TryGet(targetHostId, out var channel))
            {
                await SendMessageAsync(channel, message);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SendMessage(string targetChannel, T message)
        {
            SendMessage(new RedisChannel(targetChannel, RedisChannel.PatternMode.Literal), message);
        }

        public async Task SendMessageAsync(string targetChannel, T message)
        {
            await SendMessageAsync(new RedisChannel(targetChannel, RedisChannel.PatternMode.Literal), message);
        }

        public void SendMessage(RedisChannel channel, T message)
        {
            RedisValue value = ConvertToRedisValue(message);
            publisher.Publish(channel, value);
        }

        public async Task SendMessageAsync(RedisChannel channel, T message)
        {
            RedisValue value = ConvertToRedisValue(message);
            await publisher.PublishAsync(channel, value);
        }

        protected abstract RedisValue ConvertToRedisValue(T message);
        protected abstract T ConvertFromRedisValue(RedisValue value);
        private OnMessageReceivedCallback<T> onMessageReceivedCallback;
        public OnMessageReceivedCallback<T> OnMessageReceivedCallback
        {
            get
            {
                return onMessageReceivedCallback;
            }
            set
            {
                onMessageReceivedCallback = value;
            }
        }

        CancellationTokenSource updatingRedisCancellation;
        CancellationTokenSource updatingRedisWaitingCancellation;
        Task updatingRedis;
        ManualResetEventSlim startingLock = new ManualResetEventSlim();

        public void Start()
        {
            lock (startingLock)
            {
                if (updatingRedis != null) return;

                updatingRedisCancellation = new CancellationTokenSource();
                updatingRedisWaitingCancellation = new CancellationTokenSource();

                startingLock.Reset();

                updatingRedis = UpdateRedisAsync();
                startingLock.Wait();
            }
        }

        public void Shutdown()
        {
            lock(startingLock)
            {
                if (updatingRedis == null) return;

                updatingRedisCancellation.Cancel();
                updatingRedis.Wait();
                updatingRedis = null;
                updatingRedisCancellation = null;
                updatingRedisWaitingCancellation = null;
            }
        }

        public event EventHandler RedisServerConnectionErrorOccurred;

        async Task MainChannelPublishing(string text, CancellationToken cancellationToken)
        { 
            while (!updatingRedisCancellation.Token.IsCancellationRequested)
            {
                int retried = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await publisher.PublishAsync(mainChannel, text);
                        return;
                    }
                    catch (RedisTimeoutException)
                    {
                        if (retried > 3)
                        {
                            break;
                        }
                        else
                        {
                            retried++;
                        }
                    }
                    catch (RedisConnectionException)
                    {
                        break;
                    }
                }

                //reconnect
                RedisServerConnectionErrorOccurred?.BeginInvoke(this, EventArgs.Empty, null, null);
            }
        }

        async Task UpdateRedisAsync()
        {
            //start
            var updatingToken = updatingRedisCancellation.Token;
            await MainChannelPublishing(messageTextHello, updatingToken);

            //started
            startingLock.Set();

            var waitingToken = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token).Token;

            //keep
            var nextRefresh = DateTime.Now + hostRefreshingTime;
            while (!updatingToken.IsCancellationRequested)
            {
                nextRefresh += hostRefreshingTime;
                if (needRefreshFull)
                {
                    needRefreshFull = false;
                    await MainChannelPublishing(messageTextRefreshFull, updatingToken);
                }
                else
                {
                    await MainChannelPublishing(messageTextRefresh, updatingToken);
                }

                TimeSpan waiting = nextRefresh - DateTime.Now;
                if (waiting > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(waiting, waitingToken);
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

            //shutdown, send once only
            await publisher.PublishAsync(mainChannel, messageTextShutdown);
        }

        #region IDisposable Support
        protected virtual void OnDispose(bool disposing) { }
        
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                OnDispose(disposing);

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    subscriber.UnsubscribeAll();
                    redisConnection.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                publisher = null;
                subscriber = null;
                //redisConnection = null;
                redisDatabase = null;
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RedisClient() {
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



    class RedisClientOfString : RedisClient<string>
    {
        public RedisClientOfString(Guid clientId, string redisConfiguration, string mainChannelName, string privateChannelNamePrefix, int redisDb)
            : base(clientId, redisConfiguration, mainChannelName, privateChannelNamePrefix, redisDb)
        { }

        protected override string ConvertFromRedisValue(RedisValue value)
        {
            return value;
        }

        protected override RedisValue ConvertToRedisValue(string message)
        {
            return message;
        }
    }

    class RedisClientOfBinary : RedisClient<byte[]>
    {
        public RedisClientOfBinary(Guid clientId, string redisConfiguration, string mainChannelName, string privateChannelNamePrefix, int redisDb)
            : base(clientId, redisConfiguration, mainChannelName, privateChannelNamePrefix, redisDb)
        { }

        protected override byte[] ConvertFromRedisValue(RedisValue value)
        {
            return value;
        }

        protected override RedisValue ConvertToRedisValue(byte[] message)
        {
            return message;
        }
    }
}
