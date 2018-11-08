using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SecretNest.RemoteHub
{
    public abstract class MultiHubBase : IDisposable
    {
        public abstract Guid AddHub(IRemoteHub remoteHub);

        public abstract bool TryRemoveHub(Guid id, out IRemoteHub remoteHub);

        public abstract IReadOnlyList<KeyValuePair<Guid, IRemoteHub>> RemoteHubs { get; }

        public abstract IRemoteHub this[Guid id] { get; }

        public event EventHandler<ConnectionErrorOccurredEventArgsBase> ConnectionErrorOccurred;
        protected void RaiseConnectionErrorOccurred(object sender, ConnectionErrorOccurredEventArgsBase e)
        {
            ConnectionErrorOccurred?.Invoke(sender, e);
        }

        protected abstract void OnDispose(bool disposing);

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

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                OnDispose(disposing);

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MultiHubBase() {
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

    public class MultiHub<T> : MultiHubBase
    {
        ConcurrentDictionary<Guid, RemoteHubCapsule<T>> remoteHubs = new ConcurrentDictionary<Guid, RemoteHubCapsule<T>>();

        public override Guid AddHub(IRemoteHub remoteHub)
        {
            return AddHub((IRemoteHub<T>)remoteHub);
        }

        public Guid AddHub(IRemoteHub<T> remoteHub)
        {
            Guid id;
            RemoteHubCapsule<T> capsule = new RemoteHubCapsule<T>(remoteHub);
            if (remoteHubs.GetOrAdd(remoteHub.ClientId, capsule).RemoteHub == remoteHub)
            {
                id = remoteHub.ClientId;
                capsule.Id = id;
            }
            else
            {
                id = Guid.NewGuid();
                capsule.Id = id;
                remoteHubs[id] = capsule;
            }
            HubAdded(capsule);
            return id;
        }

        public override bool TryRemoveHub(Guid id, out IRemoteHub remoteHub)
        {
            if (remoteHubs.TryRemove(id, out var capsule))
            {
                remoteHub = capsule.RemoteHub;
                HubRemoved(capsule);
                capsule.Dispose();
                return true;
            }
            else
            {
                remoteHub = null;
                return false;
            }
        }

        public bool TryRemoveHub(Guid id, out IRemoteHub<T> remoteHub)
        {
            if (remoteHubs.TryRemove(id, out var capsule))
            {
                remoteHub = capsule.RemoteHub;
                HubRemoved(capsule);
                capsule.Dispose();
                return true;
            }
            else
            {
                remoteHub = null;
                return false;
            }
        }

        public bool TryGetHub(Guid id, out IRemoteHub<T> remoteHub)
        {
            if (remoteHubs.TryGetValue(id, out var capsule))
            {
                remoteHub = capsule.RemoteHub;
                return true;
            }
            {
                remoteHub = null;
                return false;
            }
        }

        public override IRemoteHub this[Guid id] => remoteHubs[id].RemoteHub;

        public override IReadOnlyList<KeyValuePair<Guid, IRemoteHub>> RemoteHubs => remoteHubs.Select(i => new KeyValuePair<Guid, IRemoteHub>(i.Key, i.Value.RemoteHub)).ToArray();

        public IReadOnlyList<KeyValuePair<Guid, IRemoteHub<T>>> RemoteHubsGeneric => remoteHubs.Select(i => new KeyValuePair<Guid, IRemoteHub<T>>(i.Key, i.Value.RemoteHub)).ToArray();

        public event EventHandler<ConnectionErrorOccurredEventArgs<T>> ConnectionErrorOccurredGeneric;

        void RaiseConnectionErrorOccurred(object noUsed, ConnectionErrorOccurredEventArgs<T> e)
        {
            ConnectionErrorOccurredGeneric?.Invoke(this, e);
            RaiseConnectionErrorOccurred(this, e);
        }

        OnMessageReceivedFromMultiHubCallback<T> onMessageReceivedCallback;
        public OnMessageReceivedFromMultiHubCallback<T> OnMessageReceivedCallback
        {
            get { return onMessageReceivedCallback; }
            set
            {
                onMessageReceivedCallback = value;
                foreach (var item in remoteHubs.Values)
                {
                    item.OnMessageReceivedCallback = value;
                }
            }
        }


        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                foreach(var item in remoteHubs)
                {
                    item.Value.Dispose();
                }
                remoteHubs = null;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.
        }

        void HubAdded(RemoteHubCapsule<T> capsule)
        {
            capsule.ConnectionErrorOccurred += RaiseConnectionErrorOccurred;
            capsule.OnMessageReceivedCallback = onMessageReceivedCallback;
        }

        void HubRemoved(RemoteHubCapsule<T> capsule)
        {
            capsule.ConnectionErrorOccurred -= RaiseConnectionErrorOccurred;
            capsule.OnMessageReceivedCallback = null;
        }

        public void RestartConnection(Guid id, bool keepConnectionState = false)
        {
            remoteHubs[id].RemoteHub.RestartConnection(keepConnectionState);
        }

        public void Start(Guid id)
        {
            remoteHubs[id].RemoteHub.Start();
        }

        public void Shutdown(Guid id)
        {
            remoteHubs[id].RemoteHub.Shutdown();
        }

        public void RestartAllConnections(bool keepConnectionState = false)
        {
            foreach (var item in remoteHubs.Values)
            {
                item.RemoteHub.RestartConnection(keepConnectionState);
            }
        }

        public void StartAll()
        {
            foreach (var item in remoteHubs.Values)
            {
                item.RemoteHub.Start();
            }
        }

        public void ShutdownAll()
        {
            foreach (var item in remoteHubs.Values)
            {
                item.RemoteHub.Shutdown();
            }
        }

        public void ApplyVirtualHosts(Guid id, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            remoteHubs[id].RemoteHub.ApplyVirtualHosts(settings);
        }

        public bool TryResolveVirtualHost(Guid id, Guid virtualHostId, out Guid hostId)
        {
            return remoteHubs[id].RemoteHub.TryResolveVirtualHost(virtualHostId, out hostId);
        }

        public bool SendMessage(Guid id, Guid targetHostId, T message)
        {
            return remoteHubs[id].RemoteHub.SendMessage(targetHostId, message);
        }

        public async Task<bool> SendMessageAsync(Guid id, Guid targetHostId, T message)
        {
            return await remoteHubs[id].RemoteHub.SendMessageAsync(targetHostId, message);
        }

        public void SendMessage(Guid id, string targetChannel, T message)
        {
            remoteHubs[id].RemoteHub.SendMessage(targetChannel, message);
        }

        public async Task SendMessageAsync(Guid id, string targetChannel, T message)
        {
            await remoteHubs[id].RemoteHub.SendMessageAsync(targetChannel, message);
        }

        public bool TryResolve(Guid id, Guid hostId, out RedisChannel channel)
        {
            return ((IRemoteHubRedis)remoteHubs[id].RemoteHub).TryResolve(hostId, out channel);
        }

        public void SendMessage(Guid id, RedisChannel channel, T message)
        {
            ((IRemoteHubRedis<T>)remoteHubs[id].RemoteHub).SendMessage(channel, message);
        }

        public async Task SendMessageAsync(Guid id, RedisChannel channel, T message)
        {
            await ((IRemoteHubRedis<T>)remoteHubs[id].RemoteHub).SendMessageAsync(channel, message);
        }
    }
}
