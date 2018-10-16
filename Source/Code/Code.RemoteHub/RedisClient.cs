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
        readonly ConnectionMultiplexer redisConnection;
        RedisChannel mainChannel, privateChannel;
        IDatabase redisDatabase;
        ISubscriber publisher;
        ISubscriber subscriber;
        string messageTextRefresh;
        readonly string messageTextShutdown;
        const string messageTextHello = "v1:Hello";
        TimeSpan hostTimeToLive = new TimeSpan(0, 0, 30);
        TimeSpan hostRefreshingTime = new TimeSpan(0, 0, 15);
        readonly string clientIdText;
        readonly string hostKeyPrefix;

        HostTable hostTable = new HostTable();

        protected RedisClient(Guid clientId, string redisConfiguration, string mainChannelName, string hostKeyPrefix, string privateChannelNamePrefix, int redisDb)
        {
            this.hostKeyPrefix = hostKeyPrefix;
            clientIdText = clientId.ToString("N");
            messageTextRefresh = string.Format("v1:Refresh:{0}:{1}:{2}{3}", clientIdText, (int)hostTimeToLive.TotalSeconds, privateChannelNamePrefix, clientIdText);
            messageTextShutdown = "v1:Shutdown:" + clientIdText;

            redisConnection = ConnectionMultiplexer.Connect(redisConfiguration);
            redisDatabase = redisConnection.GetDatabase(redisDb);
            mainChannel = new RedisChannel(mainChannelName, RedisChannel.PatternMode.Literal);
            privateChannel = new RedisChannel(privateChannelNamePrefix + clientIdText, RedisChannel.PatternMode.Literal);
            subscriber = redisDatabase.Multiplexer.GetSubscriber();
            subscriber.Subscribe(mainChannel, OnMainChannelReceived);
            subscriber.Subscribe(privateChannel, OnPrivateChannelReceived);

            publisher = redisDatabase.Multiplexer.GetSubscriber();
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
                messageTextRefresh = string.Format("v1:Refresh:{0}:{1}", clientIdText, (int)hostTimeToLive.TotalSeconds);
                hostRefreshingTime = new TimeSpan(hostTimeToLive.Ticks / 2);
            }
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
                    hostTable.AddOrRefresh(clientId, seconds, channelName);
                }
                else if (texts[1] == "Hello")
                {
                    var old = updatingRedisWaitingCancellation;
                    updatingRedisWaitingCancellation = new CancellationTokenSource();
                    old.Cancel();
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
                onMessageReceivedCallback(message);
            }
        }

        public bool TryResolve(Guid targetId, out RedisChannel channel)
        {
            return hostTable.TryGet(targetId, out channel);
        }

        public async Task<bool> SendMessage(Guid targetId, T message)
        {
            if (hostTable.TryGet(targetId, out RedisChannel channel))
            {
                await SendMessage(channel, message);
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task SendMessage(string targetChannel, T message)
        {
            await SendMessage(new RedisChannel(targetChannel, RedisChannel.PatternMode.Literal), message);
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
        object stateLock = new object();

        public void Start()
        {
            lock (stateLock)
            {
                if (updatingRedis != null) return;

                updatingRedisCancellation = new CancellationTokenSource();
                updatingRedisWaitingCancellation = new CancellationTokenSource();

                updatingRedis = UpdateRedisAsync();
            }
        }

        public void Shutdown()
        {
            lock(stateLock)
            {
                if (updatingRedis == null) return;

                updatingRedisCancellation.Cancel();
                updatingRedis.Wait();
                updatingRedis = null;
                updatingRedisCancellation = null;
                updatingRedisWaitingCancellation = null;
            }
        }

        async Task UpdateRedisAsync()
        {
            //start
            var updatingToken = updatingRedisCancellation.Token;
            var waitingToken = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token).Token;
            await publisher.PublishAsync(mainChannel, messageTextHello);

            //keep
            var nextRefresh = DateTime.Now + hostRefreshingTime;
            while (!updatingToken.IsCancellationRequested)
            {
                nextRefresh += hostRefreshingTime;
                await publisher.PublishAsync(mainChannel, messageTextRefresh);

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
                            waitingToken = CancellationTokenSource.CreateLinkedTokenSource(updatingToken, updatingRedisWaitingCancellation.Token).Token;
                        }
                    }
                }
            }

            //shutdown
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

    public delegate void OnMessageReceivedCallback<T>(T message); 

    class RedisClientOfString : RedisClient<string>
    {
        public RedisClientOfString(Guid clientId, string redisConfiguration, string mainChannelName, string hostKeyPrefix, string privateChannelNamePrefix, int redisDb)
            : base(clientId, redisConfiguration, mainChannelName, hostKeyPrefix, privateChannelNamePrefix, redisDb)
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
        public RedisClientOfBinary(Guid clientId, string redisConfiguration, string mainChannelName, string hostKeyPrefix, string privateChannelNamePrefix, int redisDb)
            : base(clientId, redisConfiguration, mainChannelName, hostKeyPrefix, privateChannelNamePrefix, redisDb)
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
