using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    public abstract class StreamAdapter : IRemoteHubStreamAdapter
    {
        Stream inputStream;
        Stream outputStream;
        readonly int streamRefreshingInterval;
        RemoteClientTable hostTable; //also used as startlock
        Task readingJob, writingJob;
        CancellationTokenSource shuttingdownTokenSource;
        CancellationToken shuttingdownToken;
        ManualResetEventSlim streamInReading;
        BlockingCollection<byte[]> sendingBuffers;

        /// <inheritdoc/>
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred;
        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated;
        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved;
        /// <inheritdoc/>
        public event EventHandler OnAdapterStarted;
        /// <inheritdoc/>
        public event EventHandler OnAdapterStopped;

        protected StreamAdapter(Stream inputStream, Stream outputStream, int refreshingInterval)
        {
            this.inputStream = inputStream;
            this.outputStream = outputStream;
            streamRefreshingInterval = refreshingInterval;
        }

        #region Start Stop
        void StartProcessing()
        {
            lock(hostTable)
            {
                if (shuttingdownTokenSource == null) return;
                shuttingdownTokenSource = new CancellationTokenSource();
                shuttingdownToken = shuttingdownTokenSource.Token;


            }
        }

        void StopProcessing()
        {
            lock (hostTable)
            {

            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            StartProcessing();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopProcessing();
        }

        /// <inheritdoc/>
        public bool IsStarted => shuttingdownTokenSource != null;

        /// <inheritdoc/>
        public void Stop(RemoteHubStreamAdapterForceClosingMode forceClosing, out bool isReadingStreamClosed)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void ChangeStream(Stream newInputStream, Stream newOutputStream)
        {
            if (IsStarted)
                throw new InvalidOperationException();
            lock (hostTable)
            {
                if (IsStarted)
                    throw new InvalidOperationException();

                inputStream = newInputStream;
                outputStream = newOutputStream;
            }
        }
        #endregion

        #region Stream Operating

        async Task ReadingProcessor()
        {
            
        }

        async Task WritingProcessor()
        {
            try
            {
                while (!shuttingdownToken.IsCancellationRequested)
                {
                    var buffer = sendingBuffers.Take(shuttingdownToken);
                    await outputStream.WriteAsync(buffer, 0, buffer.Length, shuttingdownToken);
                }
            }
            catch (OperationCanceledException) { }
        }

        async Task OnHelloReceivedAsync()
        {

        }

        async Task OnAddOrUpdateClientReceivedAsync(Guid senderClientId)
        {

        }

        async Task OnAddOrUpdateClientReceivedAsync(Guid senderClientId, Stream inputStream)
        {

        }

        async Task OnAddOrUpdateClientReceivedAsync(Guid senderClientId, Guid virtualHostSettingId, KeyValuePair<Guid, VirtualHostSetting>[] virtualHostSettings)
        {

        }

        async Task OnRemoveClientReceivedAsync(Guid senderClientId)
        {

        }

        async Task OnPingReceivedAsync()
        {

        }

        async Task OnLinkClosedReceivedAsync()
        {

        }
        #endregion

        /// <inheritdoc/>
        public Task AddClientAsync(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task RemoveClientAsync(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task RemoveAllClientsAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void AddClient(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void RemoveClient(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void RemoveAllClients()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllClients()
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            throw new NotImplementedException();
        }

        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId)
        {
            throw new NotImplementedException();
        }




    }
}
