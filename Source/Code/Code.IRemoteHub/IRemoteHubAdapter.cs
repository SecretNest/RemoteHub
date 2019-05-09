using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic <see cref="IRemoteHubAdapter{T}"/>.
    /// </summary>
    public interface IRemoteHubAdapter
    {

        /// <summary>
        /// Adds a local client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <returns>A task that represents the adding operating.</returns>
        Task AddClientAsync(params Guid[] clientId);

        /// <summary>
        /// Removes a local client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <returns>A task that represents the removing operating.</returns>
        Task RemoveClientAsync(params Guid[] clientId);

        /// <summary>
        /// Removes all registered local clients.
        /// </summary>
        /// <returns>A task that represents the removing operating.</returns>
        Task RemoveAllClientsAsync();

        /// <summary>
        /// Adds a local client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        void AddClient(params Guid[] clientId);

        /// <summary>
        /// Removes a local client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        void RemoveClient(params Guid[] clientId);

        /// <summary>
        /// Removes all registered local clients.
        /// </summary>
        void RemoveAllClients();

        /// <summary>
        /// Gets all current registered local clients.
        /// </summary>
        /// <returns>Ids of all current registered clients.</returns>
        IEnumerable<Guid> GetAllClients();

        /// <summary>
        /// Gets all found remote clients.
        /// </summary>
        /// <returns>Ids of all found remote clients.</returns>
        IEnumerable<Guid> GetAllRemoteClients();

        /// <summary>
        /// Starts the underlying object required operations.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the underlying object required operations.
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets whether this adapter is started or not.
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Applies virtual host settings on the client specified by id.
        /// </summary>
        /// <param name="clientId">Client which will settings be applied on.</param>
        /// <param name="settings">Virtual host settings. Null for no virtual host enabled.</param>
        void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings);

        /// <summary>
        /// Gets the virtual host settings of the client specified by id.
        /// </summary>
        /// <param name="clientId">Client to be queried.</param>
        /// <param name="settings">Virtual host settings of the client specified. <see langword="null"/> if no setting applied on this client.</param>
        /// <returns>Whether the client is found.</returns>
        bool TryGetVirtualHosts(Guid clientId, out KeyValuePair<Guid, VirtualHostSetting>[] settings);

        /// <summary>
        /// Tries to resolve virtual host by id to remote client id.
        /// </summary>
        /// <param name="virtualHostId">Virtual host id to be resolved.</param>
        /// <param name="clientId">Client id as the result.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        bool TryResolveVirtualHost(Guid virtualHostId, out Guid clientId);

        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred;

        /// <summary>
        /// Occurs while a remote client is added or changed virtual host setting.
        /// </summary>
        /// <remarks>For avoiding client status mismatched, introduced by adding and removing the same client within a tiny timespan, this event should be raised synchronously only.</remarks>
        event EventHandler<ClientWithVirtualHostSettingEventArgs> RemoteClientUpdated;

        /// <summary>
        /// Occurs while a remote client is removed.
        /// </summary>
        /// <remarks>For avoiding client status mismatched, introduced by adding and removing the same client within a tiny timespan, this event should be raised synchronously only.</remarks>
        event EventHandler<ClientIdEventArgs> RemoteClientRemoved;

        /// <summary>
        /// Occurs when this adapter started.
        /// </summary>
        event EventHandler AdapterStarted;

        /// <summary>
        /// Occurs when this adapter stopped. Also will be raised if the adapter is stopped by the request from underlying object and remote site.
        /// </summary>
        event EventHandler AdapterStopped;
    }

    /// <summary>
    /// Represents the methods of RemoteHubAdapter, which converts RemoteHub commands and events to underlying object.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public interface IRemoteHubAdapter<T> : IRemoteHubAdapter
    {
        /// <summary>
        /// Sends private message to a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        Task SendPrivateMessageAsync(Guid clientId, T message);

        /// <summary>
        /// Sends private message to a client specified by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <param name="message">Message to be sent.</param>
        void SendPrivateMessage(Guid clientId, T message);

        /// <summary>
        /// Adds a callback that will be used for dealing received private message.
        /// </summary>
        /// <param name="callback">The callback to be used for dealing received private message.</param>
        void AddOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback);

        /// <summary>
        /// Removes a callback which is used for dealing received private message.
        /// </summary>
        /// <param name="callback">The callback which is used for dealing received private message.</param>
        void RemoveOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback);

        /// <summary>
        /// Removes all callbacks which are used for dealing received private message.
        /// </summary>
        void RemoveAllOnMessageReceivedCallbacks();
    }
}
