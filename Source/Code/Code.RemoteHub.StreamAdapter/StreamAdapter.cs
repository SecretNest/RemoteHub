using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    public abstract class StreamAdapter : IDisposable, IRemoteHubAdapter
    {
        StreamReader streamReader; StreamWriter streamWriter;

        protected StreamAdapter(StreamReader reader, StreamWriter writer)
        {
            streamReader = reader;
            streamWriter = writer;
        }

        protected abstract void DisposeManagedState();
        protected abstract void DisposeUnmanagedResources();

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

                streamReader.Close();
                streamReader.Dispose();
                streamReader = null;

                streamWriter.Flush();
                streamWriter.Close();
                streamWriter.Dispose();
                streamWriter = null;
                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~StreamAdapter() {
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
