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
        Dictionary<Guid, byte[]> clients = new Dictionary<Guid, byte[]>(); //value is null if no virtual host; or value is virtual host setting id + count + setting data.
        RemoteClientTable hostTable = new RemoteClientTable();
        Task readingJob, writingJob, keepingJob;
        bool sendingNormal = false;
        bool isStopping = false;
        CancellationTokenSource shuttingdownTokenSource;
        CancellationToken shuttingdownToken;
        ManualResetEventSlim startingLock = new ManualResetEventSlim();
        BlockingCollection<byte[]> sendingBuffers;

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

        protected StreamAdapter(Stream inputStream, Stream outputStream, int refreshingIntervalInSeconds)
        {
            this.inputStream = inputStream;
            this.outputStream = outputStream;
            streamRefreshingIntervalInSeconds = refreshingIntervalInSeconds;
        }

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

                StopProcessing();

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

        void StopProcessing()
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

                readingJob.Wait();
                writingJob.Wait();
                keepingJob.Wait();

                outputStream.Close();

                if (RemoteClientRemoved != null)
                    foreach (var remoteClientId in hostTable.GetAllRemoteClientId())
                    {
                        RemoteClientRemoved(this, new ClientIdEventArgs(remoteClientId));
                    }

                readingJob = null;
                writingJob = null;
                keepingJob = null;
                inputStream = null;
                outputStream = null;
                sendingBuffers = null;

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
            StopProcessing();
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
                            OnLinkClosedReceived();
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
                        StopProcessing();
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
                StopProcessing();
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
            hostTable.AddOrUpdate(senderClientId);
            if (RemoteClientUpdated != null)
            {
                ClientWithVirtualHostSettingEventArgs e = new ClientWithVirtualHostSettingEventArgs(senderClientId, Guid.Empty, null);
                RemoteClientUpdated(this, e);
            }
        }

        void OnAddOrUpdateClientReceived(Guid senderClientId, BinaryReader inputStreamReader)
        {
            var entity = hostTable.AddOrUpdate(senderClientId, inputStreamReader);
            if (RemoteClientUpdated != null)
            {
                ClientWithVirtualHostSettingEventArgs e = new ClientWithVirtualHostSettingEventArgs(senderClientId, entity.VirtualHostSettingId, entity.VirtualHosts.ToArray());
                RemoteClientUpdated(this, e);
            }
        }

        void OnRemoveClientReceived(Guid senderClientId)
        {
            hostTable.Remove(senderClientId);
            if (RemoteClientRemoved != null)
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

        void OnLinkClosedReceived()
        {
            StopProcessing();
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

        protected void SendingPrivateMessage(Guid targetClientId, byte[] data)
        {
            int length = data.Length;
            byte[] package = new byte[data.Length + 20];
            WriteInt32ToByteArray(length, package, 0);
            Array.Copy(targetClientId.ToByteArray(), 0, package, 4, 16);
            Array.Copy(data, 0, package, 20, length);
            AddToSendingBuffer(package);
        }

        void AddToSendingBuffer(byte[] package)
        {
            try
            {
                sendingBuffers.Add(package);
            }
            catch (InvalidOperationException) //link closed.
            {
            }
            catch (NullReferenceException) //link closed.
            {
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
            lock (clients)
            {
                lock (startingLock)
                {
                    foreach (var client in clientId)
                    {
                        if (!clients.ContainsKey(client))
                        {
                            clients[client] = null;
                            if (IsStarted)
                            {
                                SendingRefreshClient(client, null);
                            }
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveClient(params Guid[] clientId)
        {
            lock (clients)
            {
                lock (startingLock)
                {
                    foreach (var client in clientId)
                    {
                        if (clients.Remove(client))
                        {
                            hostTable.Remove(client);//Remove Fake Remote Client, which may be added for Virtual Host

                            if (IsStarted)
                            {
                                SendingRemoveClient(client);
                            }
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveAllClients()
        {
            lock (clients)
            {
                if (clients.Count == 0) return;
                lock (startingLock)
                {
                    if (IsStarted)
                    {
                        var id = clients.Keys.ToArray();
                        foreach (var client in id)
                        {
                            SendingRemoveClient(client);
                        }
                    }
                    clients.Clear();
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllClients()
        {
            Guid[] id;
            lock (clients)
            {
                id = clients.Keys.ToArray();
            }
            return id;
        }

        /// <inheritdoc/>
        public IEnumerable<Guid> GetAllRemoteClients() => hostTable.GetAllRemoteClientId();

        /// <inheritdoc/>
        public void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            lock (clients)
            {
                if (clients.TryGetValue(clientId, out var currentSetting))
                {
                    if (settings == null || settings.Length == 0)
                    {
                        if (currentSetting != null)
                        {
                            clients[clientId] = null;
                            lock(startingLock)
                            {
                                if (IsStarted)
                                {
                                    SendingRefreshClient(clientId, null);
                                }
                            }

                            //Apply to sender also as fake remote client.
                            //This need to applied on StreamAdapter because main data package will not be routed back to the sender, not like the behavior in RedisAdapter.
                            hostTable.Remove(clientId); //Remove Fake Remote Client, which may be added for Virtual Host
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

                        clients[clientId] = newSetting;
                        lock (startingLock)
                        {
                            if (IsStarted)
                            {
                                SendingRefreshClient(clientId, newSetting);
                            }
                        }

                        //Apply to sender also as fake remote client.
                        //This need to applied on StreamAdapter because main data package will not be routed back to the sender, not like the behavior in RedisAdapter.
                        hostTable.AddOrUpdateLocalAsRemoteForVirtualHost(clientId, settings);
                    }
                }
                else
                {
                    throw new KeyNotFoundException("Client specified cannot be found.");
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
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid remoteClientId)
        {
            return hostTable.TryResolveVirtualHost(virtualHostId, out remoteClientId);
        }

        protected bool IsSelf(Guid clientId)
        {
            return clients.ContainsKey(clientId);
        }
    }
}
