using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Threading.Tasks;


namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic MultiHub.
    /// </summary>
    public abstract class MultiHubBase : IDisposable
    {
        /// <summary>
        /// Adds RemoteHub instance to this MultiHub instance.
        /// </summary>
        /// <param name="remoteHub">Instance of RemoteHub to be added.</param>
        /// <returns>Id of the identification of this RemoteHub instance used in MultiHub.</returns>
        /// <remarks>The id for returning is preferred from the <see cref="RemoteHub{T}.ClientId"/> property of the instance of RemoteHub to be added. If the id is existed in this instance of MultiHub, a new random id will be generated and returned.</remarks>
        public abstract Guid AddHub(IRemoteHub remoteHub);

        /// <summary>
        /// Tries to remove an RemoteHub instance from this MultiHub instance.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="remoteHub">The instance of the RemoteHub removed.</param>
        /// <returns>Result of removal.</returns>
        public abstract bool TryRemoveHub(Guid id, out IRemoteHub remoteHub);

        /// <summary>
        /// Gets the list of all added RemoteHub instances.
        /// </summary>
        public abstract IReadOnlyList<KeyValuePair<Guid, IRemoteHub>> RemoteHubs { get; }

        /// <summary>
        /// Gets the added RemoteHub instance by id specified.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract IRemoteHub this[Guid id] { get; }

        /// <summary>
        /// Occurs while connection related exception (e.g., RedisConnectionException) is thrown in main channel operating. Will not be raised for private channel operating.
        /// </summary>
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

    /// <summary>
    /// Manages multiple RemoteHub instances.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public class MultiHub<T> : MultiHubBase
    {
        ConcurrentDictionary<Guid, RemoteHubCapsule<T>> remoteHubs = new ConcurrentDictionary<Guid, RemoteHubCapsule<T>>();

        /// <summary>
        /// Gets the RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <typeparam name="TTargetType">The type of the RemoteHub instance.</typeparam>
        /// <returns>RemoteHub instance specified by id.</returns>
        protected TTargetType GetRemoteHub<TTargetType>(Guid id) where TTargetType: IRemoteHub<T>
        {
            return (TTargetType)remoteHubs[id].RemoteHub;
        }

        /// <summary>
        /// Gets all RemoteHub instances in the same type specified.
        /// </summary>
        /// <typeparam name="TTargetType">The type of the RemoteHub instance.</typeparam>
        /// <returns>All matched RemoteHub instances.</returns>
        protected IEnumerable<TTargetType> GetAllRemoteHubs<TTargetType>() where TTargetType : IRemoteHub<T>
        {
            foreach (var item in remoteHubs.Values)
            {
                if (item is TTargetType)
                    yield return (TTargetType)item.RemoteHub;
            }
        }

        /// <inheritdoc/>
        public override Guid AddHub(IRemoteHub remoteHub)
        {
            return AddHub((IRemoteHub<T>)remoteHub);
        }

        /// <summary>
        /// Adds RemoteHub instance to this MultiHub instance.
        /// </summary>
        /// <param name="remoteHub">Instance of RemoteHub to be added.</param>
        /// <returns>Id of the identification of this RemoteHub instance used in MultiHub.</returns>
        /// <remarks>The id for returning is preferred from the <see cref="RemoteHub{T}.ClientId"/> property of the instance of RemoteHub to be added. If the id is existed in this instance of MultiHub, a new random id will be generated and returned.</remarks>
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

        /// <inheritdoc/>
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

        /// <summary>
        /// Tries to remove an RemoteHub instance from this MultiHub instance.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="remoteHub">The instance of the RemoteHub removed.</param>
        /// <returns>Result of removal.</returns>
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

        /// <summary>
        /// Tries to get an RemoteHub instance by id specified.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="remoteHub">The instance of the RemoteHub matched.</param>
        /// <returns>Result of searching.</returns>
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

        /// <inheritdoc/>
        public override IRemoteHub this[Guid id] => remoteHubs[id].RemoteHub;

        /// <inheritdoc/>
        public override IReadOnlyList<KeyValuePair<Guid, IRemoteHub>> RemoteHubs => remoteHubs.Select(i => new KeyValuePair<Guid, IRemoteHub>(i.Key, i.Value.RemoteHub)).ToArray();

        /// <summary>
        /// Gets the list of all added RemoteHub instances.
        /// </summary>
        public IReadOnlyList<KeyValuePair<Guid, IRemoteHub<T>>> RemoteHubsGeneric => remoteHubs.Select(i => new KeyValuePair<Guid, IRemoteHub<T>>(i.Key, i.Value.RemoteHub)).ToArray();

        /// <summary>
        /// Occurs while connection related exception (e.g., RedisConnectionException) is thrown in main channel operating. Will not be raised for private channel operating.
        /// </summary>
        /// <remarks>This event will be raised earlier than <see cref="MultiHubBase.ConnectionErrorOccurred"/>.</remarks>
        public event EventHandler<ConnectionErrorOccurredEventArgs<T>> ConnectionErrorOccurredGeneric;

        void RaiseConnectionErrorOccurred(object noUsed, ConnectionErrorOccurredEventArgs<T> e)
        {
            ConnectionErrorOccurredGeneric?.Invoke(this, e);
            RaiseConnectionErrorOccurred(this, e);
        }

        OnMessageReceivedFromMultiHubCallback<T> onMessageReceivedCallback;

        /// <summary>
        /// Gets or sets the callback to be used for dealing received private message.
        /// </summary>
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

        /// <summary>
        /// Starts main channel processing, including keeping server status updated and alive, syncing virtual host settings, etc of the specified RemoteHub instance.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        public void Start(Guid id)
        {
            remoteHubs[id].RemoteHub.Start();
        }

        /// <summary>
        /// Stops main channel processing of the specified RemoteHub instance. Private channel sending and receiving will not be affected but the resolving functions cannot be executed.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        public void Shutdown(Guid id)
        {
            remoteHubs[id].RemoteHub.Shutdown();
        }

        /// <summary>
        /// Starts main channel processing, including keeping server status updated and alive, syncing virtual host settings, etc of all RemoteHub instances.
        /// </summary>
        public void StartAll()
        {
            foreach (var item in remoteHubs.Values)
            {
                item.RemoteHub.Start();
            }
        }

        /// <summary>
        /// Stops main channel processing of all RemoteHub instances. Private channel sending and receiving will not be affected but the resolving functions cannot be executed.
        /// </summary>
        public void ShutdownAll()
        {
            foreach (var item in remoteHubs.Values)
            {
                item.RemoteHub.Shutdown();
            }
        }

        /// <summary>
        /// Applies the virtual hosts setting for the client specified.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="settings">Virtual host settings. Key is virtual host id; Value is setting related to the virtual host specified.</param>
        public void ApplyVirtualHosts(Guid id, params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            remoteHubs[id].RemoteHub.ApplyVirtualHosts(settings);
        }

        /// <summary>
        /// Tries to resolve virtual host by id to host id of the RemoteHub instance specified.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="virtualHostId">Virtual host id.</param>
        /// <param name="hostId">Host id.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        public bool TryResolveVirtualHost(Guid id, Guid virtualHostId, out Guid hostId)
        {
            return remoteHubs[id].RemoteHub.TryResolveVirtualHost(virtualHostId, out hostId);
        }

        /// <summary>
        /// Sends a message to the target host specified by target host id through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="targetHostId">Target host id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>Whether the resolving from target host id is succeeded or not.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message, if the RemoteHub instance used is based on Redis operating.</remarks>
        public bool SendMessage(Guid id, Guid targetHostId, T message)
        {
            return remoteHubs[id].RemoteHub.SendMessage(targetHostId, message);
        }

        /// <summary>
        /// Creates a task that sends a message to the target host specified by target host id through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="targetHostId"></param>
        /// <param name="message"></param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message, if the RemoteHub instance used is based on Redis operating.</remarks>
        public async Task<bool> SendMessageAsync(Guid id, Guid targetHostId, T message)
        {
            return await remoteHubs[id].RemoteHub.SendMessageAsync(targetHostId, message);
        }

        /// <summary>
        /// Sends a message to the target host specified by private channel name through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message, if the RemoteHub instance used is based on Redis operating.</remarks>
        public void SendMessage(Guid id, string targetChannel, T message)
        {
            remoteHubs[id].RemoteHub.SendMessage(targetChannel, message);
        }

        /// <summary>
        /// Creates a task that sends a message to the target host specified by private channel name through RemoteHub instance specified by id.
        /// </summary>
        /// <param name="id">Id of the identification of the RemoteHub instance used in MultiHub.</param>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        /// <remarks><see cref="RedisServerException"/> and <see cref="RedisTimeoutException"/> may be thrown when the Redis error occurred while sending message, if the RemoteHub instance used is based on Redis operating.</remarks>
        public async Task SendMessageAsync(Guid id, string targetChannel, T message)
        {
            await remoteHubs[id].RemoteHub.SendMessageAsync(targetChannel, message);
        }

    }
}
