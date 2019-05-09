using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic <see cref="StreamAdapter{T}"/>, which converts between RemoteHub commands and stream.
    /// </summary>
    public abstract class StreamAdapter : IDisposable, IRemoteHubStreamAdapter
    {
        Stream inputStream;
        Stream outputStream;
        readonly int streamRefreshingIntervalInSeconds;
        readonly ConcurrentDictionary<Guid, byte[]> clients = new ConcurrentDictionary<Guid, byte[]>(); //value is null if no virtual host; or value is virtual host setting id + count + setting data.
        readonly ClientTable clientTable = new ClientTable();
        Task readingJob, writingJob, keepingJob;
        bool sendingNormal = false;
        bool isStopping = false;
        CancellationTokenSource shuttingdownTokenSource;
        CancellationToken shuttingdownToken;
        readonly ManualResetEventSlim startingLock = new ManualResetEventSlim(); //also used as a lock when need to query IsStarted
        BlockingCollection<byte[]> sendingBuffers;

        /// <summary>
        /// Will be called when a private message is received from the stream.
        /// </summary>
        /// <param name="targetClientId">Client id of the receiver.</param>
        /// <param name="dataPackage">Data package of the message.</param>
        protected abstract void OnPrivateMessageReceived(Guid targetClientId, byte[] dataPackage);

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

        /// <summary>
        /// Initializes an instance of StreamAdapter.
        /// </summary>
        /// <param name="inputStream">Stream for reading.</param>
        /// <param name="outputStream">Stream for writing.</param>
        /// <param name="refreshingIntervalInSeconds">The interval in seconds before sending a data package for keeping it alive when streams are idle.</param>
        protected StreamAdapter(Stream inputStream, Stream outputStream, int refreshingIntervalInSeconds)
        {
            this.inputStream = inputStream;
            this.outputStream = outputStream;
            streamRefreshingIntervalInSeconds = refreshingIntervalInSeconds;
        }

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
                StopProcessing(false);

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

        #region Start Stop
        void StartProcessing()
        {
            lock (startingLock) //only for starting lock
            {
                if (readingJob != null) return;
                isStopping = false;
                sendingNormal = true;
                startingLock.Reset();
                shuttingdownTokenSource = new CancellationTokenSource();
                shuttingdownToken = shuttingdownTokenSource.Token;
                sendingBuffers = new BlockingCollection<byte[]>();

                readingJob = Task.Run(() => ReadingProcessor());
                writingJob = Task.Run(async () => await WritingProcessorAsync());
                keepingJob = Task.Run(async () => await KeepingProcessorAsync());

                SendingRefreshAllClients();

                startingLock.Wait();

                AdapterStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        void StopProcessing(bool fromReadingJob)
        {
            if (isStopping) return;
            lock (startingLock) //only for starting lock
            {
                if (readingJob == null) return;

                if (isStopping) return;
                isStopping = true;

                if (sendingNormal)
                {
                    sendingBuffers.Add(new byte[] { 255 }); //Link closed.
                    sendingBuffers.CompleteAdding();

                    while (!sendingBuffers.IsCompleted && sendingNormal)
                    {
                        Task.Delay(100).Wait();
                    }
                }

                shuttingdownTokenSource.Cancel();
                inputStream.Close(); //Need to close 1st to make readingJob closable.

                if (!fromReadingJob)
                    readingJob.Wait();
                writingJob.Wait();
                keepingJob.Wait();

                outputStream.Close();

                if (RemoteClientRemoved != null)
                    foreach (var remoteClientId in GetAllRemoteClients().ToArray())
                    {
                        RemoteClientRemoved(this, new ClientIdEventArgs(remoteClientId));
                    }

                readingJob = null;
                writingJob = null;
                keepingJob = null;
                inputStream = null;
                outputStream = null;
                sendingBuffers = null;

                shuttingdownTokenSource.Dispose();

                //OnAdapterStopped will be raised at the end of receiving procedure.
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            StartProcessing();
        }

        /// <summary>
        /// Stops the underlying object required operations. Streams will be closed also.
        /// </summary>
        public void Stop()
        {
            StopProcessing(false);
        }

        /// <inheritdoc/>
        public bool IsStarted => readingJob != null;

        /// <inheritdoc/>
        public void ChangeStream(Stream newInputStream, Stream newOutputStream)
        {
            if (IsStarted)
                throw new InvalidOperationException();
            lock (startingLock)
            {
                if (IsStarted)
                    throw new InvalidOperationException();

                inputStream = newInputStream;
                outputStream = newOutputStream;
            }
        }
        #endregion

        #region Stream Operating
        DateTime lastStreamWorkingTime = DateTime.MinValue;

        void ReadingProcessor()
        {
            using (BinaryReader binaryReader = new BinaryReader(inputStream))
            {
                byte commandCode;
                try
                {
                    while (!shuttingdownToken.IsCancellationRequested)
                    {
                        commandCode = binaryReader.ReadByte();

                        lastStreamWorkingTime = DateTime.Now;

                        if (commandCode <= 127) //Private channel data
                        {
                            int length = GetInt32FromBytes(commandCode, binaryReader.ReadByte(), binaryReader.ReadByte(), binaryReader.ReadByte());
                            OnPrivateMessageReceived(length, binaryReader);
                        }
                        else if (commandCode == 254) //Ping
                        {
                            OnPingReceived();
                        }
                        else if (commandCode == 253) //Pong
                        {
                            //Do nothing
                        }
                        else if (commandCode == 130) //Add or update client with virtual host setting
                        {
                            var clientId = binaryReader.ReadGuid();
                            OnAddOrUpdateClientReceived(clientId, binaryReader);
                        }
                        else if (commandCode == 129) //Add or update client without virtual host setting
                        {
                            var clientId = binaryReader.ReadGuid();
                            OnAddOrUpdateClientReceived(clientId);
                        }
                        else if (commandCode == 131) //Remove
                        {
                            var clientId = binaryReader.ReadGuid();
                            OnRemoveClientReceived(clientId);
                        }
                        else if (commandCode == 128) //Hello
                        {
                            OnHelloReceived();
                        }
                        else if (commandCode == 255) //Link closed
                        {
                            binaryReader.Close();
                            StopProcessing(true);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!shuttingdownToken.IsCancellationRequested) //Or, it is closed by terminating reading process.
                    {
                        if (ConnectionErrorOccurred != null)
                        {
                            ConnectionExceptionEventArgs e = new ConnectionExceptionEventArgs(ex, true, false);
                            ConnectionErrorOccurred(this, e);
                        }
                        StopProcessing(true);
                    }
                }
            }

            AdapterStopped?.Invoke(this, EventArgs.Empty);
        }

        async Task WritingProcessorAsync()
        {
            try
            {
                //Start
                lastStreamWorkingTime = DateTime.Now;
                await outputStream.WriteAsync(new byte[] { 128 }, 0, 1, shuttingdownToken); //Hello
                startingLock.Set();

                //Looping
                while (!shuttingdownToken.IsCancellationRequested)
                {
                    var buffer = sendingBuffers.Take(shuttingdownToken);
                    lastStreamWorkingTime = DateTime.Now;
                    await outputStream.WriteAsync(buffer, 0, buffer.Length, shuttingdownToken);
                }
            }
            catch (InvalidOperationException) { } //Finish adding, called only from StopProcessing
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                sendingNormal = false;
                if (ConnectionErrorOccurred != null)
                {
                    ConnectionExceptionEventArgs e = new ConnectionExceptionEventArgs(ex, true, false);
                    ConnectionErrorOccurred(this, e);
                }
                StopProcessing(false);
            }
        }

        async Task KeepingProcessorAsync()
        {
            try
            {
                while (!shuttingdownToken.IsCancellationRequested)
                {
                    DateTime previousTime = lastStreamWorkingTime;
                    int delay = streamRefreshingIntervalInSeconds * 1000 - (int)(DateTime.Now - previousTime).TotalMilliseconds;
                    if (delay > 0)
                        await Task.Delay(delay, shuttingdownToken);

                    if (shuttingdownToken.IsCancellationRequested) break;
                    if (previousTime == lastStreamWorkingTime)
                    {
                        lastStreamWorkingTime = DateTime.Now;
                        AddToSendingBuffer(new byte[] { 254 }); //Sending ping
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        void OnHelloReceived()
        {
            SendingRefreshAllClients();
            //Task.Run(SendingRefreshAllClients);
        }

        void SendingRefreshAllClients()
        {
            foreach (var client in clients)
            {
                SendingRefreshClient(client.Key, client.Value);
            }
        }

        void OnAddOrUpdateClientReceived(Guid senderClientId)
        {
            clientTable.AddOrUpdate(senderClientId);
            if (RemoteClientUpdated != null)
            {
                ClientWithVirtualHostSettingEventArgs e = new ClientWithVirtualHostSettingEventArgs(senderClientId, Guid.Empty, null);
                RemoteClientUpdated(this, e);
            }
        }

        void OnAddOrUpdateClientReceived(Guid senderClientId, BinaryReader inputStreamReader)
        {
            var entity = clientTable.AddOrUpdate(senderClientId, inputStreamReader);
            if (RemoteClientUpdated != null)
            {
                ClientWithVirtualHostSettingEventArgs e = new ClientWithVirtualHostSettingEventArgs(senderClientId, entity.VirtualHostSettingId, entity.VirtualHosts.ToArray());
                RemoteClientUpdated(this, e);
            }
        }

        void OnRemoveClientReceived(Guid senderClientId)
        {
            if (clientTable.Remove(senderClientId) && RemoteClientRemoved != null)
            {
                ClientIdEventArgs e = new ClientIdEventArgs(senderClientId);
                RemoteClientRemoved(this, e);
            }
        }

        void OnPingReceived()
        {
            try
            {
                sendingBuffers.Add(new byte[] { 253 });
            }
            catch { }
        }

        void OnPrivateMessageReceived(int length, BinaryReader inputStreamReader)
        {
            var targetClientId = inputStreamReader.ReadGuid();
            var dataPackage = inputStreamReader.ReadBytes(length);
            OnPrivateMessageReceived(targetClientId, dataPackage);
        }

        void SendingRefreshClient(Guid targetClientId, byte[] setting)
        {
            byte[] package;
            if (setting == null)
            {
                package = new byte[17];
                package[0] = 129;
            }
            else
            {
                package = new byte[17 + setting.Length];
                package[0] = 130;
                Array.Copy(setting, 0, package, 17, setting.Length);
            }
            Array.Copy(targetClientId.ToByteArray(), 0, package, 1, 16);
            AddToSendingBuffer(package);
        }

        void SendingRemoveClient(Guid targetClientId)
        {
            byte[] package = new byte[17];
            package[0] = 131;
            Array.Copy(targetClientId.ToByteArray(), 0, package, 1, 16);
            AddToSendingBuffer(package);
        }

        /// <summary>
        /// Sends the private message.
        /// </summary>
        /// <param name="targetClientId">Client id of the receiver.</param>
        /// <param name="data">Data package of the message.</param>
        protected void SendingPrivateMessage(Guid targetClientId, byte[] data)
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            int length = data.Length;
            byte[] package = new byte[data.Length + 20];
            WriteInt32ToByteArray(length, package, 0);
            Array.Copy(targetClientId.ToByteArray(), 0, package, 4, 16);
            Array.Copy(data, 0, package, 20, length);
            if (!AddToSendingBuffer(package))
            {
                throw new InvalidOperationException();
            }
        }

        bool AddToSendingBuffer(byte[] package)
        {
            try
            {
                sendingBuffers.Add(package);
                return true;
            }
            catch (InvalidOperationException) //link closed.
            {
                return false;
            }
            catch (NullReferenceException) //link closed.
            {
                return false;
            }
        }
        #endregion

        /// <inheritdoc/>
        public Task AddClientAsync(params Guid[] clientId)
        {
            return Task.Run(() => AddClient(clientId));
        }

        /// <inheritdoc/>
        public Task RemoveClientAsync(params Guid[] clientId)
        {
            return Task.Run(() => RemoveClient(clientId));
        }

        /// <inheritdoc/>
        public Task RemoveAllClientsAsync()
        {
            return Task.Run(() => RemoveAllClients());
        }

        /// <inheritdoc/>
        public void AddClient(params Guid[] clientId)
        {
            lock (startingLock)
            {
                foreach (var client in clientId)
                {
                    if (clients.TryAdd(client, null))
                    {
                        clientTable.AddOrUpdate(client);
                        if (IsStarted)
                        {
                            SendingRefreshClient(client, null);
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveClient(params Guid[] clientId)
        {
            lock (startingLock)
            {
                foreach (var client in clientId)
                {
                    if (clients.TryRemove(client, out _))
                    {
                        if (clientTable.Remove(client) && IsStarted)
                        {
                            SendingRemoveClient(client);
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveAllClients()
        {
            lock (startingLock)
            {
                var id = clients.Keys.ToArray();
                if (id.Length == 0) return;
                RemoveClient(id);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllClients()
        {
            Guid[] id = clients.Keys.ToArray();
            return id;
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllRemoteClients()
        {
            HashSet<Guid> localClients = new HashSet<Guid>();
            foreach (var id in clients.Keys.ToArray())
            {
                localClients.Add(id);
            }
            Guid[] result = clientTable.GetAllRemoteClientsId().ToArray();
            foreach (var id in result)
            {
                if (localClients.Contains(id))
                    continue;
                else
                    yield return id;
            }
        }

        /// <inheritdoc/>
        public void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            lock (startingLock)
            {
                if (!clients.TryGetValue(clientId, out var currentSetting))
                {
                    throw new KeyNotFoundException("Client specified cannot be found.");
                }

                if (settings == null || settings.Length == 0)
                {
                    if (currentSetting != null)
                    {
                        if (!clients.TryUpdate(clientId, null, currentSetting))
                        {
                            throw new InvalidOperationException("Client specified is removed or virtual hosts setting is changed.");
                        }

                        if (IsStarted)
                        {
                            SendingRefreshClient(clientId, null);
                        }

                        clientTable.ClearVirtualHosts(clientId);
                    }
                }
                else
                {
                    int length = settings.Length;
                    if (length > 65536)
                        throw new InvalidDataException("Too many (>64k) settings applied.");
                    byte[] newSetting = new byte[18 + 24 * length];
                    Array.Copy(Guid.NewGuid().ToByteArray(), newSetting, 16);
                    newSetting[16] = (byte)(length / 256);
                    newSetting[17] = (byte)(length % 256);
                    int location = 18;

                    //This block looks like a shit:
                    //var settingEnumerator = settings.GetEnumerator();
                    //settingEnumerator.MoveNext();
                    //while (true)
                    //{
                    //    var item = (KeyValuePair<Guid, VirtualHostSetting>)settingEnumerator.Current;
                    //    Array.Copy(item.Key.ToByteArray(), 0, newSetting, location, 16);
                    //    WriteInt32ToByteArray(item.Value.Priority, newSetting, location + 16);
                    //    WriteInt32ToByteArray(item.Value.Weight, newSetting, location + 20);

                    //    if (settingEnumerator.MoveNext())
                    //    {
                    //        location += 24;
                    //    }
                    //    else
                    //    {
                    //        break;
                    //    }
                    //}

                    foreach (var item in settings)
                    {
                        Array.Copy(item.Key.ToByteArray(), 0, newSetting, location, 16);
                        WriteInt32ToByteArray(item.Value.Priority, newSetting, location + 16);
                        WriteInt32ToByteArray(item.Value.Weight, newSetting, location + 20);
                        location += 24;
                    }

                    if (!clients.TryUpdate(clientId, newSetting, currentSetting))
                    {
                        throw new InvalidOperationException("Client specified is removed or virtual hosts setting is changed.");
                    }
                    if (IsStarted)
                    {
                        SendingRefreshClient(clientId, newSetting);
                    }

                    clientTable.AddOrUpdate(clientId, settings);
                }
            }
        }

        void WriteInt32ToByteArray(int value, byte[] buffer, int location)
        {
            buffer[location] = (byte)(value >> 24);
            buffer[location + 1] = (byte)(value << 8 >> 24);
            buffer[location + 2] = (byte)(value << 16 >> 24);
            buffer[location + 3] = (byte)(value << 24 >> 24);
        }

        int GetInt32FromBytes(byte b1, byte b2, byte b3, byte b4)
        {
            return ((int)b1 << 24) + ((int)b2 << 16) + ((int)b3 << 8) + (int)b4;
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId)
        {
            return clientTable.TryResolveVirtualHost(virtualHostId, out clientId);
        }

        /// <inheritdoc/>
        public bool TryGetVirtualHosts(Guid clientId, out KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            var entity = clientTable.Get(clientId);
            if (entity == null)
            {
                settings = default;
                return false;
            }
            else
            {
                if (entity.IsVirtualHostsDisabled)
                {
                    settings = default;
                }
                else
                {
                    settings = entity.GetVirtualHosts();
                }
                return true;
            }
        }

        /// <summary>
        /// Gets whether the client specified is registered as local.
        /// </summary>
        /// <param name="clientId">Client to check.</param>
        /// <returns>Whether the client is registered as local.</returns>
        protected bool IsSelf(Guid clientId)
        {
            return clients.ContainsKey(clientId);
        }


    }
}
