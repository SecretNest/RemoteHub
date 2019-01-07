using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    public abstract class StreamAdapter : IRemoteHubAdapter
    {
        readonly BinaryReader streamReader;
        readonly BinaryReader streamWriter;
        readonly int streamRefreshingInterval;
        RemoteClientTable hostTable;

        /// <inheritdoc/>
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred;
        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated;
        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved;

        protected StreamAdapter(BinaryReader reader, BinaryReader writer, int refreshingInterval)
        {
            streamReader = reader;
            streamWriter = writer;
            streamRefreshingInterval = refreshingInterval;
        }

        public Task AddClientAsync(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveClientAsync(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAllClientsAsync()
        {
            throw new NotImplementedException();
        }

        public void AddClient(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        public void RemoveClient(params Guid[] clientId)
        {
            throw new NotImplementedException();
        }

        public void RemoveAllClients()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Guid> GetAllClients()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();

        }

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
