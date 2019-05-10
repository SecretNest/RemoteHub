using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Handles the host state and message transportation.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) are supported.</typeparam>
    public class RemoteHubSwitchDirect<T> : IDisposable, IRemoteHub<T>, IRemoteHubAdapter<byte[]>
    {
        readonly Guid clientId;
        bool isStarted; //RemoteHub and Adapter shared the started state.
        object isStartedChangingLock = new object();

        /// <summary>
        /// Initializes an instance of RemoteHubSwitchDirect.
        /// </summary>
        /// <param name="clientId">Client id of the local RemoteHub.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="encoding">The encoder for converting between string and byte array. Default value is Encoding.Default. Will be ignored if type is not string.</param>
        public RemoteHubSwitchDirect(Guid clientId, OnMessageReceivedCallback<T> onMessageReceivedCallback, Encoding encoding = null)
        {
            this.clientId = clientId;
            clientTable.AddOrUpdate(clientId);
            clientSidePrivateMessageCallbackHelper = new PrivateMessageCallbackHelper<T>(onMessageReceivedCallback);
            adapterSidePrivateMessageCallbackHelper = new PrivateMessageCallbackHelper<byte[]>(null);
            valueConverter = ValueConverter<T>.Create(encoding);
        }

        #region RemoteHub
        /// <inheritdoc/>
        public Guid ClientId => clientId;

        /// <summary>
        /// Gets whether this instance is started or not.
        /// </summary>
        /// <remarks>The state of this instance is shared with adapter. The state of the adapter linked will always be the same as the state of this instance.</remarks>
        public bool IsStarted => isStarted;

        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        /// <remarks>This event will never be raised due to no breakable connection will be used in this class.</remarks>
#pragma warning disable CS0067 
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred; //will never happen
#pragma warning restore CS0067
        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated;
        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved;

        /// <summary>
        /// Occurs when this instance started.
        /// </summary>
        /// <remarks>The state of this instance is shared with adapter. The state of the adapter linked will always be the same as the state of this instance. No matter the cause of the state changed to started, this event will be raised always.</remarks>
        public event EventHandler Started;

        /// <summary>
        /// Occurs when this instance stopped. Also will be raised if the instance is stopped by the request from underlying object and remote site.
        /// </summary>
        /// <remarks>The state of this instance is shared with adapter. The state of the adapter linked will always be the same as the state of this instance. No matter the cause of the state changed to stopped, this event will be raised always.</remarks>
        public event EventHandler Stopped;

        /// <inheritdoc/>
        public void SendMessage(Guid clientId, T message)
        {
            if (clientId == this.clientId)
            {
                SendMessageDirectToClient(clientId, message);
            }
            else if (isStarted)
            {
                SendMessageThroughAdapter(clientId, message);
            }
            else
            {
                throw new InvalidOperationException(); //not started
            }
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(Guid clientId, T message)
        {
            if (clientId == this.clientId)
            {
                await SendMessageDirectToClientAsync(clientId, message);
            }
            else if (isStarted)
            {
                await SendMessageThroughAdapterAsync(clientId, message);
            }
            else
            {
                throw new InvalidOperationException(); //not started
            }
        }

        /// <inheritdoc/>
        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            var currentSetting = clientTable.Get(clientId);

            if (settings == null || settings.Length == 0)
            {
                if (currentSetting.IsVirtualHostsDisabled)
                    return;
                else
                {
                    clientTable.ClearVirtualHosts(clientId);
                    if (isStarted && ToSwitch_RemoteClientUpdated != null)
                    {
                        ToSwitch_RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, Guid.Empty, null));
                    }
                }
            }
            else
            {
                var newSettingId = Guid.NewGuid();
                clientTable.AddOrUpdate(clientId, newSettingId, settings);
                if (isStarted && ToSwitch_RemoteClientUpdated != null)
                {
                    ToSwitch_RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, newSettingId, settings));
                }
            }
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId)
        {
            return clientTable.TryResolveVirtualHost(virtualHostId, out clientId);
        }

        /// <inheritdoc/>
        public bool TryGetVirtualHosts(Guid clientId, out KeyValuePair<Guid, VirtualHostSetting>[] settings) //remotehub and adapter shared
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
        /// Starts instance operating.
        /// </summary>
        /// <remarks>The state of this instance is shared with adapter. The state of the adapter linked will always be the same as the state of this instance. Calling this method will set the state of adapter to started also.</remarks>
        public void Start() //remotehub and adapter shared
        {
            if (isStarted) return;
            lock (isStartedChangingLock)
            {
                if (isStarted) return;
                isStarted = true;

                if (ToSwitch_RemoteClientUpdated != null)
                {
                    var localClientEntity = clientTable.Get(clientId);
                    if (localClientEntity.IsVirtualHostsDisabled)
                    {
                        ToSwitch_RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, Guid.Empty, null));
                    }
                    else
                    {
                        ToSwitch_RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, localClientEntity.VirtualHostSettingId, localClientEntity.VirtualHosts.ToArray()));
                    }
                }

                Started?.Invoke(this, EventArgs.Empty);
                ToSwitch_Started?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Stops instance operating.
        /// </summary>
        /// <remarks>The state of this instance is shared with adapter. The state of the adapter linked will always be the same as the state of this instance. Calling this method will set the state of adapter to stopped also.</remarks>
        public void Stop() //remotehub and adapter shared
        {
            if (!isStarted) return;
            Guid[] allRemoteClients;
            lock (isStartedChangingLock)
            {
                if (!isStarted) return;
                isStarted = false;

                ToSwitch_RemoteClientRemoved?.Invoke(this, new ClientIdEventArgs(clientId));

                allRemoteClients = FromAdapter_GetAllRemoteClients().ToArray();
                foreach (var remoteClientId in allRemoteClients)
                {
                    clientTable.Remove(remoteClientId);
                }

                Stopped?.Invoke(this, EventArgs.Empty);
                ToSwitch_Stopped?.Invoke(this, EventArgs.Empty);
            }

            if (RemoteClientRemoved != null)
                foreach (var remoteClientId in allRemoteClients)
                {
                    RemoteClientRemoved(this, new ClientIdEventArgs(remoteClientId));
                }
        }

        #endregion

        #region Routing
        PrivateMessageCallbackHelper<T> clientSidePrivateMessageCallbackHelper;
        PrivateMessageCallbackHelper<byte[]> adapterSidePrivateMessageCallbackHelper;
        ValueConverter<T> valueConverter;
        ClientTable clientTable = new ClientTable();

        void SendMessageDirectToClient(Guid targetClientId, T messageForClient)
        {
            clientSidePrivateMessageCallbackHelper.CallAndForget(targetClientId, messageForClient);
        }

        async Task SendMessageDirectToClientAsync(Guid targetClientId, T messageForClient)
        {
            await clientSidePrivateMessageCallbackHelper.CallAsync(targetClientId, messageForClient);
        }

        void SendMessageFromAdapterToClient(Guid targetClientId, byte[] messageForAdapter)
        {
            var messageForClient = valueConverter.ConvertFromMessage(messageForAdapter);
            SendMessageDirectToClient(targetClientId, messageForClient);
        }

        async Task SendMessageFromAdapterToClientAsync(Guid targetClientId, byte[] messageForAdapter)
        {
            var messageForClient = valueConverter.ConvertFromMessage(messageForAdapter);
            await SendMessageDirectToClientAsync(targetClientId, messageForClient);
        }

        void SendMessageThroughAdapter(Guid targetClientId, T messageForClient)
        {
            var messageForAdapter = valueConverter.ConvertToMessage(messageForClient);
            adapterSidePrivateMessageCallbackHelper.CallAndForget(targetClientId, messageForAdapter);
        }

        async Task SendMessageThroughAdapterAsync(Guid targetClientId, T messageForClient)
        {
            var messageForAdapter = valueConverter.ConvertToMessage(messageForClient);
            await adapterSidePrivateMessageCallbackHelper.CallAsync(targetClientId, messageForAdapter);
        }

        #endregion

        #region Adapter
        //NOTE: In this region, all members are for connecting to switch.
        //Clients are the clients in switch, not local. And the RemoteClient is the client id of this instance.

        bool IRemoteHubAdapter.IsStarted => throw new NotImplementedException();

        event EventHandler<ConnectionExceptionEventArgs> IRemoteHubAdapter.ConnectionErrorOccurred //will never be raised
        {
            add { } remove { }
        }

        event EventHandler<ClientWithVirtualHostSettingEventArgs> ToSwitch_RemoteClientUpdated;

        event EventHandler<ClientWithVirtualHostSettingEventArgs> IRemoteHubAdapter.RemoteClientUpdated
        {
            add
            {
                ToSwitch_RemoteClientUpdated += value;
            }

            remove
            {
                ToSwitch_RemoteClientUpdated -= value;
            }
        }

        event EventHandler<ClientIdEventArgs> ToSwitch_RemoteClientRemoved;

        event EventHandler<ClientIdEventArgs> IRemoteHubAdapter.RemoteClientRemoved
        {
            add
            {
                ToSwitch_RemoteClientRemoved += value;
            }

            remove
            {
                ToSwitch_RemoteClientRemoved -= value;
            }
        }

        event EventHandler ToSwitch_Started;

        event EventHandler IRemoteHubAdapter.AdapterStarted
        {
            add
            {
                ToSwitch_Started += value;
            }

            remove
            {
                ToSwitch_Started -= value;
            }
        }

        event EventHandler ToSwitch_Stopped;

        event EventHandler IRemoteHubAdapter.AdapterStopped
        {
            add
            {
                ToSwitch_Stopped += value;
            }

            remove
            {
                ToSwitch_Stopped -= value;
            }
        }

        async Task IRemoteHubAdapter<byte[]>.SendPrivateMessageAsync(Guid clientId, byte[] message)
        {
            await SendMessageFromAdapterToClientAsync(clientId, message);
        }

        void IRemoteHubAdapter<byte[]>.SendPrivateMessage(Guid clientId, byte[] message)
        {
            SendMessageFromAdapterToClient(clientId, message);
        }

        void IRemoteHubAdapter<byte[]>.AddOnMessageReceivedCallback(OnMessageReceivedCallback<byte[]> callback)
        {
            lock (adapterSidePrivateMessageCallbackHelper)
            {
                adapterSidePrivateMessageCallbackHelper.AddCallback(callback);
            }
        }

        void IRemoteHubAdapter<byte[]>.RemoveOnMessageReceivedCallback(OnMessageReceivedCallback<byte[]> callback)
        {
            lock (adapterSidePrivateMessageCallbackHelper)
            {
                adapterSidePrivateMessageCallbackHelper.RemoveCallback(callback);
            }
        }

        void IRemoteHubAdapter<byte[]>.RemoveAllOnMessageReceivedCallbacks()
        {
            lock (adapterSidePrivateMessageCallbackHelper)
            {
                adapterSidePrivateMessageCallbackHelper.RemoveAllCallbacks();
            }
        }

        Task IRemoteHubAdapter.AddClientAsync(params Guid[] clientId)
        {
            return Task.Run(() => FromAdapter_AddClient(clientId));
        }

        Task IRemoteHubAdapter.RemoveClientAsync(params Guid[] clientId)
        {
            return Task.Run(() => FromAdapter_RemoveClient(clientId));
        }

        Task IRemoteHubAdapter.RemoveAllClientsAsync()
        {
            return Task.Run(() => FromAdapter_RemoveAllClients());
        }

        void IRemoteHubAdapter.AddClient(params Guid[] clientId)
        {
            FromAdapter_AddClient(clientId);
        }

        void FromAdapter_AddClient(Guid[] clientId)
        {
            foreach (var client in clientId)
            {
                if (client != this.clientId && clientTable.Get(client) == null)
                {
                    clientTable.AddOrUpdate(client);
                    if (isStarted)
                    {
                        RemoteClientUpdated?.Invoke(this, new ClientWithVirtualHostSettingEventArgs(client, Guid.Empty, null));
                    }
                }
            }
        }

        void IRemoteHubAdapter.RemoveClient(params Guid[] clientId)
        {
            FromAdapter_RemoveClient(clientId);
        }

        void FromAdapter_RemoveClient(Guid[] clientId)
        {
            foreach (var client in clientId)
            {
                if (client != this.clientId && clientTable.Remove(client) && isStarted)
                {
                    RemoteClientRemoved?.Invoke(this, new ClientIdEventArgs(client));
                }
            }
        }

        void IRemoteHubAdapter.RemoveAllClients()
        {
            FromAdapter_RemoveAllClients();
        }

        void FromAdapter_RemoveAllClients()
        {
            Guid[] all = FromAdapter_GetAllRemoteClients();
            if (all.Length == 0) return;
            FromAdapter_RemoveClient(all);
        }

        IEnumerable<Guid> IRemoteHubAdapter.GetAllClients()
        {
            Guid[] id = clientTable.GetAllClientsId().ToArray();
            return id;
        }

        IEnumerable<Guid> IRemoteHubAdapter.GetAllRemoteClients()
        {
            Guid[] all = FromAdapter_GetAllRemoteClients();
            return all;
        }

        Guid[] FromAdapter_GetAllRemoteClients()
        {
            Guid[] all;
            lock (isStartedChangingLock)
            {
                all = clientTable.GetAllClientsId().Where(i => i != clientId).ToArray();
            }
            return all;
        }

        /// <inheritdoc/>
        void IRemoteHubAdapter.ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            var currentSetting = clientTable.Get(clientId);

            var existed = currentSetting != null;

            if (existed && (settings == null || settings.Length == 0))
            {
                if (currentSetting.IsVirtualHostsDisabled)
                    return;
                else
                {
                    clientTable.ClearVirtualHosts(clientId);
                    if (isStarted && RemoteClientUpdated != null)
                    {
                        RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, Guid.Empty, null));
                    }
                }
            }
            else
            {
                var newSettingId = Guid.NewGuid();
                clientTable.AddOrUpdate(clientId, newSettingId, settings);
                if (isStarted && RemoteClientUpdated != null)
                {
                    RemoteClientUpdated(this, new ClientWithVirtualHostSettingEventArgs(clientId, newSettingId, settings));
                }
            }
        }
        #endregion

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
                    Stop();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RemoteHubSwitchDirect()
        // {
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
    }
}
