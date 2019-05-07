using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Handles the host state and message transportation.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) are supported.</typeparam>
    public class RemoteHubOverStream<T> : IDisposable, IRemoteHubOverStream, IRemoteHub<T>
    {
        StreamAdapter<T> streamAdapter;
        readonly Guid clientId;

        /// <inheritdoc/>
        public Guid ClientId => clientId;
        /// <inheritdoc/>
        public bool IsStarted => streamAdapter.IsStarted;

        /// <inheritdoc/>
        public event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated
        {
            add
            {
                streamAdapter.RemoteClientUpdated += value;
            }
            remove
            {
                streamAdapter.RemoteClientUpdated -= value;
            }
        }

        /// <inheritdoc/>
        public event EventHandler<ClientIdEventArgs> RemoteClientRemoved
        {
            add
            {
                streamAdapter.RemoteClientRemoved += value;
            }
            remove
            {
                streamAdapter.RemoteClientRemoved -= value;
            }
        }

        /// <inheritdoc/>
        public event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred
        {
            add
            {
                streamAdapter.ConnectionErrorOccurred += value;
            }
            remove
            {
                streamAdapter.ConnectionErrorOccurred -= value;
            }
        }

        /// <inheritdoc/>
        public event EventHandler Started
        {
            add
            {
                streamAdapter.AdapterStarted += value;
            }
            remove
            {
                streamAdapter.AdapterStarted -= value;
            }
        }

        /// <inheritdoc/>
        public event EventHandler Stopped
        {
            add
            {
                streamAdapter.AdapterStopped += value;
            }
            remove
            {
                streamAdapter.AdapterStopped -= value;
            }
        }

        /// <summary>
        /// Initializes an instance of RemoteHubOverStream.
        /// </summary>
        /// <param name="clientId">Client id of the local RemoteHub.</param>
        /// <param name="inputStream">Stream for reading.</param>
        /// <param name="outputStream">Stream for writing.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="refreshingIntervalInSeconds">The interval in seconds before sending a data package for keeping it alive when streams are idle. Default value is 60 seconds.</param>
        /// <param name="encoding">The encoder for converting between string and byte array. Default value is Encoding.Default. Will be ignored if type is not string.</param>
        public RemoteHubOverStream(Guid clientId, Stream inputStream, Stream outputStream, OnMessageReceivedCallback<T> onMessageReceivedCallback, int refreshingIntervalInSeconds = 60, Encoding encoding = null)
        {
            streamAdapter = new StreamAdapter<T>(inputStream, outputStream, onMessageReceivedCallback, refreshingIntervalInSeconds, encoding);
            this.clientId = clientId;
            streamAdapter.AddClient(clientId);
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
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    streamAdapter.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RemoteHub() {
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

        /// <inheritdoc/>
        public void ChangeStream(Stream newInputStream, Stream newOuputStream)
        {
            streamAdapter.ChangeStream(newInputStream, newOuputStream);
        }

        /// <inheritdoc/>
        public void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            streamAdapter.ApplyVirtualHosts(clientId, settings);
        }

        /// <inheritdoc/>
        public bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId)
        {
            return streamAdapter.TryResolveVirtualHost(virtualHostId, out hostId);
        }

        /// <inheritdoc/>
        public void Start()
        {
            streamAdapter.Start();
        }

        /// <summary>
        /// Stops the underlying object required operations. Streams will be closed also.
        /// </summary>
        public void Stop()
        {
            streamAdapter.Stop();
        }

        /// <inheritdoc/>
        public void SendMessage(Guid clientId, T message)
        {
            streamAdapter.SendPrivateMessage(clientId, message);
        }

        /// <inheritdoc/>
        public Task SendMessageAsync(Guid clientId, T message)
        {
            return streamAdapter.SendPrivateMessageAsync(clientId, message);
        }
    }
}
