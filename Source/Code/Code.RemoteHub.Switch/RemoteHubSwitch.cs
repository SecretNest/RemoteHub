using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Connects RemoteHub Adapters by using data switching to receive, process and forward data to the destination client.
    /// </summary>
    /// <remarks><para>Only <see cref="IRemoteHubAdapter{byte[]}"/> is supported as adapter.</para>
    /// <para>Though only byte array is only the acceptable type parameter of adapter, all adapter which encoded private messages as byte array could be supported due to no private message will be decoded in RemoteHub Switch.</para>
    /// <para>For example, a stream, which is connected to a instance of <see cref="IRemoteHubAdapter{string}"/> in another endpoint, can be handled by a instance of <see cref="IRemoteHubAdapter{byte[]}"/> for linked to this RemoteHub Switch instance. </para></remarks>
    public class RemoteHubSwitch
    {
        ConcurrentDictionary<Guid, IRemoteHubAdapter<byte[]>> adapterOfClients = new ConcurrentDictionary<Guid, IRemoteHubAdapter<byte[]>>(); //lock when changing, not reading
        ConcurrentDictionary<IRemoteHubAdapter<byte[]>, HashSet<Guid>> adapters = new ConcurrentDictionary<IRemoteHubAdapter<byte[]>, HashSet<Guid>>(); //HashSet need to be locked while remote client id of this adapter changing.

        /// <summary>
        /// Occurs while an adapter is added.
        /// </summary>
        public event EventHandler<AdapterEventArgs> AdapterAdded;

        /// <summary>
        /// Occurs while an adapter is removed.
        /// </summary>
        public event EventHandler<AdapterEventArgs> AdapterRemoved;

        #region Event - RemoteClientAdded
        /// <summary>
        /// Occurs while a remote client is added.
        /// </summary>
        /// <remarks>For avoiding client status mismatched, introduced by adding and removing the same client within a tiny timespan, this event should be processed synchronously only.</remarks>
        public event EventHandler<ClientIdWithAdapterEventArgs> RemoteClientAdded;
        void OnAdapterRemoteClientUpdated(object sender, ClientIdEventArgs e)
        {
            var adapter = (IRemoteHubAdapter<byte[]>)sender;
            var remoteClientId = e.ClientId;

            if (adapters.TryGetValue(adapter, out var idList))
            {
                lock (idList)
                {
                    if (adapters.ContainsKey(adapter))
                    {
                        idList.Add(remoteClientId);

                        AddAdapterToAdapterOfClients(remoteClientId, adapter);
                    }
                }
            }
        }
        #endregion

        #region Event - RemoteClientRemoved
        /// <summary>
        /// Occurs while a remote client is removed.
        /// </summary>
        /// <remarks>For avoiding client status mismatched, introduced by adding and removing the same client within a tiny timespan, this event should be processed synchronously only.</remarks>
        public event EventHandler<ClientIdWithAdapterEventArgs> RemoteClientRemoved;
        void OnAdapterRemoteClientRemoved(object sender, ClientIdEventArgs e)
        {
            var adapter = (IRemoteHubAdapter<byte[]>)sender;
            var remoteClientId = e.ClientId;

            if (adapters.TryGetValue(adapter, out var idList))
            {
                lock (idList)
                {
                    if (adapters.ContainsKey(adapter))
                    {
                        RemoveAdapterFromAdapterOfClients(remoteClientId, adapter);

                        idList.Remove(remoteClientId);
                    }
                }
            }
        }
        #endregion

        #region Event - ConnectionErrorOccurred
        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        public event EventHandler<ConnectionExceptionWithAdapterEventArgs> ConnectionErrorOccurred
        {
            add
            {
                if (ConnectionErrorOccurredInternal == null && !adapters.IsEmpty)
                {
                    foreach(var adapter in adapters.Keys)
                    {
                        adapter.ConnectionErrorOccurred += OnAdapterConnectionErrorOccurred;
                    }
                }

                ConnectionErrorOccurredInternal += value;
            }
            remove
            {
                ConnectionErrorOccurredInternal -= value;

                if (ConnectionErrorOccurredInternal == null && !adapters.IsEmpty)
                {
                    foreach (var adapter in adapters.Keys)
                    {
                        adapter.ConnectionErrorOccurred -= OnAdapterConnectionErrorOccurred;
                    }
                }
            }
        }

        event EventHandler<ConnectionExceptionWithAdapterEventArgs> ConnectionErrorOccurredInternal;

        void OnAdapterConnectionErrorOccurred(object sender, ConnectionExceptionEventArgs e)
        {
            var adapter = (IRemoteHubAdapter<byte[]>)sender;
            ConnectionErrorOccurredInternal(this, new ConnectionExceptionWithAdapterEventArgs(e, adapter));
        }
        #endregion

        #region Event - AdapterStarted
        /// <summary>
        /// Occurs when an adapter started.
        /// </summary>
        public event EventHandler<AdapterEventArgs> AdapterStarted
        {
            add
            {
                if (AdapterStartedInternal == null && !adapters.IsEmpty)
                {
                    foreach (var adapter in adapters.Keys)
                    {
                        adapter.AdapterStarted += OnAdapterStarted;
                    }
                }

                AdapterStartedInternal += value;
            }
            remove
            {
                AdapterStartedInternal -= value;

                if (AdapterStartedInternal == null && !adapters.IsEmpty)
                {
                    foreach (var adapter in adapters.Keys)
                    {
                        adapter.AdapterStarted -= OnAdapterStarted;
                    }
                }
            }
        }

        event EventHandler<AdapterEventArgs> AdapterStartedInternal;

        void OnAdapterStarted(object sender, EventArgs e)
        {
            var adapter = (IRemoteHubAdapter<byte[]>)sender;
            AdapterStartedInternal(this, new AdapterEventArgs(adapter));
        }
        #endregion

        #region Event - AdapterStopped
        /// <summary>
        /// Occurs when an adapter stopped. Also will be raised if the adapter is stopped by the request from underlying object and remote site.
        /// </summary>
        public event EventHandler<AdapterEventArgs> AdapterStopped
        {
            add
            {
                if (AdapterStoppedInternal == null && !adapters.IsEmpty)
                {
                    foreach (var adapter in adapters.Keys)
                    {
                        adapter.AdapterStopped += OnAdapterStopped;
                    }
                }

                AdapterStoppedInternal += value;
            }
            remove
            {
                AdapterStoppedInternal -= value;

                if (AdapterStoppedInternal == null && !adapters.IsEmpty)
                {
                    foreach (var adapter in adapters.Keys)
                    {
                        adapter.AdapterStopped -= OnAdapterStopped;
                    }
                }
            }
        }

        event EventHandler<AdapterEventArgs> AdapterStoppedInternal;

        void OnAdapterStopped(object sender, EventArgs e)
        {
            var adapter = (IRemoteHubAdapter<byte[]>)sender;
            AdapterStoppedInternal(this, new AdapterEventArgs(adapter));
        }

        #endregion

        #region Adapters and Remote Clients
        /// <summary>
        /// Gets all remote clients.
        /// </summary>
        /// <returns>Ids of all found remote clients.</returns>
        public IEnumerable<Guid> GetAllRemoteClients()
        {
            return adapterOfClients.Keys;
        }

        /// <summary>
        /// Gets all attached adapters.
        /// </summary>
        /// <returns>All attached adapters.</returns>
        public IEnumerable<IRemoteHubAdapter<byte[]>> GetAllAdapters()
        {
            return adapters.Keys;
        }

        /// <summary>
        /// Gets all remote clients registered with the adapter and being used, not replaced by other adapters, in this RemoteHub Switch instance.
        /// </summary>
        /// <param name="adapter">The adapter for being querying.</param>
        /// <returns>Ids of the remote clients registered with the adapter specified and being used, not replaced by other adapters, in this RemoteHub Switch instance. <see langword="null"/> will be returned if the adapter specified is not registered with this RemoteHub Switch instance.</returns>
        public IEnumerable<Guid> GetActiveRemoteClientsOfAdapter(IRemoteHubAdapter<byte[]> adapter)
        {
            if (adapters.TryGetValue(adapter, out var set))
            {
                lock (set)
                {
                    return set.ToArray();
                }
            }
            else
            {
                return null;
            }
        }

        void AddAdapterToAdapterOfClients(Guid remoteClientId, IRemoteHubAdapter<byte[]> adapter)
        {
            lock (adapterOfClients) //lock while changing
            {
                if (adapterOfClients.TryGetValue(remoteClientId, out var old))
                {
                    if (old != adapter)
                    {
                        adapterOfClients[remoteClientId] = adapter;

                        if (adapters.TryGetValue(old, out var idList))
                        {
                            lock (idList)
                            {
                                idList.Remove(remoteClientId);
                            }
                        }
                    }
                    return;
                }
                else
                {
                    adapterOfClients[remoteClientId] = adapter;

                    RemoteClientAdded?.Invoke(this, new ClientIdWithAdapterEventArgs(remoteClientId, adapter));
                }
            }

            //broadcast
            var allAdapters = adapters.Keys.ToArray();
            foreach (var target in allAdapters)
            {
                if (target != adapter)
                    target.AddClient(remoteClientId);
            }
        }

        void RemoveAdapterFromAdapterOfClients(Guid remoteClientId, IRemoteHubAdapter<byte[]> adapter)
        {
            lock (adapterOfClients)
            {
                if (adapterOfClients.TryGetValue(remoteClientId, out var old) && old == adapter)
                {
                    adapterOfClients.TryRemove(remoteClientId, out _);

                    RemoteClientRemoved?.Invoke(this, new ClientIdWithAdapterEventArgs(remoteClientId, adapter));
                }
                else
                {
                    return;
                }
            }

            //broadcast
            var allAdapters = adapters.Keys.ToArray();
            foreach (var target in allAdapters)
            {
                if (target != adapter)
                    target.RemoveClient(remoteClientId);
            }
        }

        /// <summary>
        /// Attaches an adapter to this switch. Starts the adapter if the switch is started.
        /// </summary>
        /// <param name="adapter">Adapter to be attached.</param>
        public void AddAdapter(IRemoteHubAdapter<byte[]> adapter)
        {
            var idList = new HashSet<Guid>();
            lock (idList)
            {
                if (adapters.TryAdd(adapter, idList))
                {
                    adapter.AddOnMessageReceivedCallback(OnPrivateMessageReceivedCallback);

                    adapter.RemoteClientUpdated += OnAdapterRemoteClientUpdated;
                    adapter.RemoteClientRemoved += OnAdapterRemoteClientRemoved;

                    foreach (var remoteClientId in adapter.GetAllRemoteClients())
                    {
                        AddAdapterToAdapterOfClients(remoteClientId, adapter);
                        idList.Add(remoteClientId);
                    }
                }
                else
                {
                    return;
                }
            }

            if (ConnectionErrorOccurredInternal != null)
            {
                adapter.ConnectionErrorOccurred += OnAdapterConnectionErrorOccurred;
            }
            if (AdapterStartedInternal != null)
            {
                adapter.AdapterStarted += OnAdapterStarted;
            }
            if (AdapterStoppedInternal != null)
            {
                adapter.AdapterStopped += OnAdapterStopped;
            }

            //get clients id
            Guid[] allClientId;
            lock (adapterOfClients)
            {
                allClientId = adapterOfClients.Where(i => i.Value != adapter).Select(i => i.Key).ToArray();
            }
            adapter.AddClient(allClientId);

            adapter.Start();

            AdapterAdded?.Invoke(this, new AdapterEventArgs(adapter));
        }

        /// <summary>
        /// Attaches multiple adapters to this switch. Starts the adapters if the switch is started.
        /// </summary>
        /// <param name="adapters">Adapters to be attached.</param>
        public void AddAdapters(params IRemoteHubAdapter<byte[]>[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
            {
                foreach (var adapter in adapters)
                    AddAdapter(adapter);
            }
        }

        /// <summary>
        /// Removes an adapter from this switch.
        /// </summary>
        /// <param name="adapter">Adapter to be removed.</param>
        /// <param name="stopAdapter">Whether the adapter should be stopped after removal. Default value is <see langword="false"/>.</param>
        public void RemoveAdapter(IRemoteHubAdapter<byte[]> adapter, bool stopAdapter = false)
        {
            adapter.RemoteClientUpdated -= OnAdapterRemoteClientUpdated;
            adapter.RemoteClientRemoved -= OnAdapterRemoteClientRemoved;

            adapter.RemoveOnMessageReceivedCallback(OnPrivateMessageReceivedCallback);

            if (adapters.TryGetValue(adapter, out var idList))
            {
                lock (idList)
                {
                    if (adapters.TryRemove(adapter, out _))
                    {
                        foreach (var remoteClientId in idList)
                        {
                            RemoveAdapterFromAdapterOfClients(remoteClientId, adapter);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else
            {
                return;
            }

            if (stopAdapter)
            {
                adapter.Stop(); //don't need to remove clients due to stopping applied.
            }
            else
            {
                //remove clients from the adapter being removed.

                Guid[] allClientId;
                lock (adapterOfClients)
                {
                    allClientId = adapterOfClients.Where(i => i.Value != adapter).Select(i => i.Key).ToArray();
                }

                adapter.RemoveClient(allClientId);
            }

            adapter.ConnectionErrorOccurred -= OnAdapterConnectionErrorOccurred;
            adapter.AdapterStarted -= OnAdapterStarted;
            adapter.AdapterStopped -= OnAdapterStopped;

            AdapterRemoved?.Invoke(this, new AdapterEventArgs(adapter));
        }

        /// <summary>
        /// Removes adapters from this switch.
        /// </summary>
        /// <param name="adapters">Adapters to be removed.</param>
        /// <param name="stopAdapter">Whether the adapters should be stopped after removal. Default value is <see langword="false"/>.</param>
        public void RemoveAdapter(IEnumerable<IRemoteHubAdapter<byte[]>> adapters, bool stopAdapter = false)
        {
            if (adapters != null)
                foreach (var adapter in adapters)
                    RemoveAdapter(adapter, stopAdapter);
        }

        /// <summary>
        /// Removes adapters from this switch.
        /// </summary>
        /// <param name="stopAdapter">Whether the adapters should be stopped after removal.</param>
        /// <param name="adapters">Adapters to be removed.</param>
        public void RemoveAdapters(bool stopAdapter, params IRemoteHubAdapter<byte[]>[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
                foreach (var adapter in adapters)
                    RemoveAdapter(adapter, stopAdapter);
        }

        /// <summary>
        /// Removes all adapters from this switch.
        /// </summary>
        /// <param name="stopAdapter">Whether the adapters should be stopped after removal. Default value is <see langword="false"/>.</param>
        public void RemoveAllAdapters(bool stopAdapter = false)
        {
            var adaptersArray = adapters.Keys.ToArray();

            foreach(var adapter in adaptersArray)
            {
                RemoveAdapter(adapter, stopAdapter);
            }

            adapters.Clear();
            adapterOfClients.Clear();
        }
        #endregion

        void OnPrivateMessageReceivedCallback(Guid receiverClientId, byte[] message)
        {
            if (adapterOfClients.TryGetValue(receiverClientId, out var adapter))
            {
                adapter.SendPrivateMessage(receiverClientId, message);
            }
        }
    }
}
