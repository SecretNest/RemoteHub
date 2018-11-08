using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class RemoteHubCapsule<T> : IDisposable
    {
        public IRemoteHub<T> RemoteHub { get; }
        public Guid Id { get; set; }

        public RemoteHubCapsule(IRemoteHub<T> remoteHub)
        {
            RemoteHub = remoteHub;
            remoteHub.ConnectionErrorOccurred += RemoteHub_ConnectionErrorOccurred;
            remoteHub.OnMessageReceivedCallback = OnMessageReceived;
        }

        private void RemoteHub_ConnectionErrorOccurred(object sender, EventArgs e)
        {
            ConnectionErrorOccurred(null, new ConnectionErrorOccurredEventArgs<T>(Id, RemoteHub));
        }

        public event EventHandler<ConnectionErrorOccurredEventArgs<T>> ConnectionErrorOccurred;

        public OnMessageReceivedFromMultiHubCallback<T> OnMessageReceivedCallback { get; set; }

        void OnMessageReceived(Guid clientId, T message)
        {
            OnMessageReceivedCallback?.Invoke(Id, clientId, message);
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
                    RemoteHub.ConnectionErrorOccurred -= RemoteHub_ConnectionErrorOccurred;
                    RemoteHub.OnMessageReceivedCallback = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RemoteHubCapsule() {
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
    }
}
