using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SecretNest.RemoteHub.StreamRelay
{
    public abstract class StreamRelayServer
    {

        protected StreamRelayServer()
        {

        }

        public void AddOrUpdateUser(Guid clientId, Stream stream, int clientAliveIntervalInSecond)
        {

        }

        public void ApplyUserStream(Guid clientId, Stream stream)
        {

        }

        public void RemoveUserStream(Guid clientId)
        {

        }

        public void RemoveUser(Guid clientId)
        {
        }

        [ThreadStatic] static byte[] bufferForGettingLength = new byte[4];
        [ThreadStatic] static SpinLock lockForGettingLength = new SpinLock();
        protected static long GetDataPackageLength(Stream stream)
        {
            bool gotLock = false;
            try
            {
                lockForGettingLength.Enter(ref gotLock);
                if (stream.Read(bufferForGettingLength, 0, 4) != 4)
                    return -1;
                return BitConverter.ToUInt32(bufferForGettingLength, 0);
            }
            finally
            {
                if (gotLock) lockForGettingLength.Exit();
            }
        }



        public event EventHandler<ClientIdEventArgs> UserAddedOrUpdated;
        public event EventHandler<ClientIdEventArgs> UserRemoved;
        public event EventHandler<ClientIdEventArgs> UserTimedout;

        public event EventHandler<ClientIdEventArgs> UserConnected;
        public event EventHandler<ClientIdEventArgs> UserDisconnected;

        public event EventHandler<ClientIdEventArgs> UserMessagePooled;
    }

    public delegate IRemoteHub<T> RemoteHubInstanceRequestingCallback<T>(Guid clientId);
    public delegate void RemoteHubInstanceShutdownCallback<T>(Guid clientId, IRemoteHub<T> remoteHub);

    public abstract class StreamRelayServer<T> : StreamRelayServer
    {
        RemoteHubInstanceRequestingCallback<T> remoteHubInstanceRequestingCallback;
        RemoteHubInstanceShutdownCallback<T> remoteHubInstanceShutdownCallback;

        ConcurrentDictionary<Guid, IRemoteHub<T>> remoteHubs = new ConcurrentDictionary<Guid, IRemoteHub<T>>();

        public StreamRelayServer(RemoteHubInstanceRequestingCallback<T> remoteHubInstanceRequestingCallback, RemoteHubInstanceShutdownCallback<T> remoteHubInstanceShutdownCallback)
        {
            this.remoteHubInstanceRequestingCallback = remoteHubInstanceRequestingCallback;
            this.remoteHubInstanceShutdownCallback = remoteHubInstanceShutdownCallback;
        }

        IRemoteHub<T> GetRemoteHub(Guid clientId)
        {
            return remoteHubs.GetOrAdd(clientId, CreateRemoteHub);
        }

        IRemoteHub<T> CreateRemoteHub(Guid clientId)
        {
            var instance = remoteHubInstanceRequestingCallback(clientId);
            instance.Start();
            return instance;
        }
    }
}
