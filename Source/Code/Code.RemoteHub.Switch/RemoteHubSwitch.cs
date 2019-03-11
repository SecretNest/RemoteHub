using SecretNest.RemoteHub;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    /// <summary>
    /// Connects RemoteHub Adapters by using data switching to receive, process and forward data to the destination client.
    /// </summary>
    public class RemoteHubSwitch
    {
        ConcurrentDictionary<Guid, List<IRemoteHubAdapter<byte[]>>> adapterOfClients = new ConcurrentDictionary<Guid, List<IRemoteHubAdapter<byte[]>>>();

        /// <summary>
        /// Occurs while a remote client is added.
        /// </summary>
        public event EventHandler<ClientIdWithAdapterEventArgs> RemoteClientAdded;

        /// <summary>
        /// Occurs while a remote client is removed.
        /// </summary>
        public event EventHandler<ClientIdWithAdapterEventArgs> RemoteClientRemoved;

        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        public event EventHandler<ConnectionExceptionWithAdapterEventArgs> ConnectionErrorOccurred;

        /// <summary>
        /// Occurs when an adapter started.
        /// </summary>
        public event EventHandler<AdapterEventArgs> OnAdapterStarted;

        /// <summary>
        /// Occurs when an adapter stopped. Also will be raised if the adapter is stopped by the request from underlying object and remote site.
        /// </summary>
        public event EventHandler<AdapterEventArgs> OnAdapterStopped;

        /// <summary>
        /// Gets all remote clients.
        /// </summary>
        /// <returns>Ids of all found remote clients.</returns>
        public IEnumerable<Guid> GetAllRemoteClients()
        {
            return adapterOfClients.Keys;
        }

        public void Start()
        {

        }

        public void Stop()
        {

        }

        public void AddAdapter(IRemoteHubAdapter<byte[]> adapter)
        {

        }

        public void AddAdapters(params IRemoteHubAdapter<byte[]>[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
                foreach (var adapter in adapters)
                    AddAdapter(adapter);
        }

        public void RemoveAdapter(IRemoteHubAdapter<byte[]> adapter)
        {

        }

        public void RemoveAdapters(params IRemoteHubAdapter<byte[]>[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
                foreach (var adapter in adapters)
                    RemoveAdapter(adapter);
        }

        public void RemoveAllAdapters()
        {

        }
    }
}
