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
    public abstract class StreamAdapter : IRemoteHubStreamAdapter
    {
        Stream inputStream;
        Stream outputStream;
        readonly int streamRefreshingIntervalInSeconds;
        Dictionary<Guid, byte[]> clients = new Dictionary<Guid, byte[]>(); //value is null if no virtual host; or value is virtual host setting id + count + setting data.
        RemoteClientTable hostTable; //also used as startlock
        Task readingJob, writingJob, keepingJob;
        CancellationTokenSource shuttingdownTokenSource;
        CancellationToken shuttingdownToken;
        ManualResetEventSlim streamInIdling;
        BlockingCollection<byte[]> sendingBuffers;

        protected abstract void OnPrivateMessageReceived(Guid targetClientId, byte[] dataPackage);

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

        protected StreamAdapter(Stream inputStream, Stream outputStream, int refreshingIntervalInSeconds)
        {
            this.inputStream = inputStream;
            this.outputStream = outputStream;
            streamRefreshingIntervalInSeconds = refreshingIntervalInSeconds;
        }

        #region Start Stop
        void StartProcessing()
        {
            lock(hostTable) //only for staring lock
            {
                if (readingJob != null) return;
                shuttingdownTokenSource = new CancellationTokenSource();
                shuttingdownToken = shuttingdownTokenSource.Token;

                readingJob = Task.Run(() => ReadingProcessor());
                writingJob = WritingProcessorAsync();
                keepingJob = KeepingProcessorAsync();

                OnAdapterStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        void StopProcessing()
        {
            lock (hostTable) //only for staring lock
            {
                if (readingJob == null) return;

                shuttingdownTokenSource.Cancel();
                inputStream.Close(); //Need to close 1st to make readingJob closable.

                readingJob.Wait();
                writingJob.Wait();
                keepingJob.Wait();

                outputStream.Close();

                readingJob = null;
                writingJob = null;
                keepingJob = null;
                inputStream = null;
                outputStream = null;

                //OnAdapterStopped will be raised at the end of receiving procedure.
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
        public bool IsStarted => readingJob != null;

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
        DateTime lastStreamWorkingTime = DateTime.MinValue;

        void ReadingProcessor()
        {
            byte[] length4 = new byte[4];
            using (BinaryReader binaryReader = new BinaryReader(inputStream))
            {
                byte commandCode;
                try
                {
                    while (!shuttingdownToken.IsCancellationRequested)
                    {
                        commandCode = binaryReader.ReadByte();
                        streamInIdling.Reset();

                        lastStreamWorkingTime = DateTime.Now;

                        if (commandCode <= 127) //Private channel data
                        {
                            length4[0] = commandCode;
                            length4[1] = binaryReader.ReadByte();
                            length4[2] = binaryReader.ReadByte();
                            length4[3] = binaryReader.ReadByte();

                            int length = BitConverter.ToInt32(length4, 0);
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
                        StopProcessing();
                        if (ConnectionErrorOccurred != null)
                        {
                            ConnectionExceptionEventArgs e = new ConnectionExceptionEventArgs(ex, true, false);
                            ConnectionErrorOccurred(this, e);
                        }
                    }
                }
            }

            OnAdapterStopped?.Invoke(this, EventArgs.Empty);
        }

        async Task WritingProcessorAsync()
        {
            try
            {
                while (!shuttingdownToken.IsCancellationRequested)
                {
                    var buffer = sendingBuffers.Take(shuttingdownToken);
                    lastStreamWorkingTime = DateTime.Now;
                    await outputStream.WriteAsync(buffer, 0, buffer.Length, shuttingdownToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopProcessing();
                if (ConnectionErrorOccurred != null)
                {
                    ConnectionExceptionEventArgs e = new ConnectionExceptionEventArgs(ex, true, false);
                    ConnectionErrorOccurred(this, e);
                }
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
                    await Task.Delay(delay, shuttingdownToken);

                    if (shuttingdownToken.IsCancellationRequested) break;
                    if (previousTime == lastStreamWorkingTime)
                    {
                        lastStreamWorkingTime = DateTime.Now;
                        sendingBuffers.Add(new byte[] { 254 }); //Sending ping
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        void OnHelloReceived()
        {
            foreach(var client in clients)
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
            sendingBuffers.Add(new byte[] { 253 });
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
            sendingBuffers.Add(package);
        }

        void SendingRemoveClient(Guid targetClientId)
        {
            byte[] package = new byte[17];
            package[0] = 131;
            Array.Copy(targetClientId.ToByteArray(), 0, package, 1, 16);
            sendingBuffers.Add(package);
        }

        protected void SendingPrivateMessage(Guid targetClientId, byte[] data)
        {
            int length = data.Length;
            byte[] package = new byte[data.Length + 20];
            Array.Copy(BitConverter.GetBytes(length), package, 4);
            Array.Copy(targetClientId.ToByteArray(), 0, package, 4, 16);
            Array.Copy(data, 0, package, 20, length);
            sendingBuffers.Add(package);
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
                lock (hostTable)
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
                lock (hostTable)
                {
                    foreach (var client in clientId)
                    {
                        if (clients.Remove(client))
                        {
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
                lock (hostTable)
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
                            lock(hostTable)
                            {
                                if (IsStarted)
                                {
                                    SendingRefreshClient(clientId, null);
                                }
                            }
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
                        lock (hostTable)
                        {
                            if (IsStarted)
                            {
                                SendingRefreshClient(clientId, null);
                            }
                        }
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

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid remoteClientId)
        {
            return hostTable.TryResolveVirtualHost(virtualHostId, out remoteClientId);
        }

    }
}
