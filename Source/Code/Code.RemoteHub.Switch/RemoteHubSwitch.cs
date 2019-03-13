using SecretNest.RemoteHub;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Code.RemoteHub.Switch
{
    /// <summary>
    /// Connects RemoteHub Adapters by using data switching to receive, process and forward data to the destination client.
    /// </summary>
    public class RemoteHubSwitch
    {
        //ConcurrentHashSet<IRemoteHubAdapter<byte[]>> allAdapters = new ConcurrentHashSet<IRemoteHubAdapter<byte[]>>();
        ConcurrentDictionary<Guid, HashSet<IRemoteHubAdapter<byte[]>>> adapterOfClients = new ConcurrentDictionary<Guid, HashSet<IRemoteHubAdapter<byte[]>>>();

        /// <summary>
        /// Occurs while a remote client is added.
        /// </summary>
        public event EventHandler<ClientIdWithAdapterEventArgs> OnRemoteClientAdded;
        void OnAdapterRemoteClientAdded(object sender, ClientIdEventArgs e)
        {
            var list = adapterOfClients.GetOrAdd(e.ClientId, (id) => new HashSet<IRemoteHubAdapter<byte[]>>());

            lock (list)
            {
                if (list.Count == 0 & list.Add((IRemoteHubAdapter<byte[]>)sender)) //Use &, not &&: The Add method need to be called always
                {
                    if (OnRemoteClientAdded != null)
                    {
                        Task.Run(() => OnRemoteClientAdded(this, new ClientIdWithAdapterEventArgs(e.ClientId, (IRemoteHubAdapter<byte[]>)sender)));
                    }
                }
            }
        }

        /// <summary>
        /// Occurs while a remote client is removed.
        /// </summary>
        public event EventHandler<ClientIdWithAdapterEventArgs> OnRemoteClientRemoved;
        void OnAdapterRemoteClientRemoved(object sender, ClientIdEventArgs e)
        {
            if (adapterOfClients.TryGetValue(e.ClientId, out var list))
            {
                lock (list)
                {
                    if (list.Count == 1 & list.Remove((IRemoteHubAdapter<byte[]>)sender)) //Use &, not &&: The Remove method need to be called always
                    {
                        adapterOfClients.TryRemove(e.ClientId, out _);
                        if (OnRemoteClientRemoved != null)
                        {
                            Task.Run(() => OnRemoteClientRemoved(this, new ClientIdWithAdapterEventArgs(e.ClientId, (IRemoteHubAdapter<byte[]>)sender)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        public event EventHandler<ConnectionExceptionWithAdapterEventArgs> OnConnectionErrorOccurred;

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
