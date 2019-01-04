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
        /// Adds a client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <returns>A task that represents the adding operating.</returns>
        Task AddClientAsync(params Guid[] clientId);

        /// <summary>
        /// Removes a client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        /// <returns>A task that represents the removing operating.</returns>
        Task RemoveClientAsync(params Guid[] clientId);

        /// <summary>
        /// Removes all clients.
        /// </summary>
        /// <returns>A task that represents the removing operating.</returns>
        Task RemoveAllClientsAsync();

        /// <summary>
        /// Adds a client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        void AddClient(params Guid[] clientId);

        /// <summary>
        /// Removes a client by id.
        /// </summary>
        /// <param name="clientId">Client id.</param>
        void RemoveClient(params Guid[] clientId);

        /// <summary>
        /// Removes all clients.
        /// </summary>
        void RemoveAllClients();

        /// <summary>
        /// Gets all current registered clients.
        /// </summary>
        /// <returns>Ids of all current registered clients.</returns>
        IEnumerable<Guid> GetAllClients();

        /// <summary>
        /// Starts the underlying object required operations.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the underlying object required operations.
        /// </summary>
        void Stop();

        /// <summary>
        /// Applies virtual host settings on the client specified by id.
        /// </summary>
        /// <param name="clientId">Client which will settings be applied on.</param>
        /// <param name="settings">Virtual host settings. Null for no virtual host enabled.</param>
        void ApplyVirtualHosts(Guid clientId, params KeyValuePair<Guid, VirtualHostSetting>[] settings);

        /// <summary>
        /// Tries to resolve virtual host by id to host id.
        /// </summary>
        /// <param name="virtualHostId">Virtual host id.</param>
        /// <param name="hostId">Host id.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId);
    }

    /// <summary>
    /// Represents the methods of RemoteHubAdapter, which converts RemoteHub commands and events to underlying object.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public interface IRemoteHubAdapter<T> : IRemoteHubAdapter
    {
        /// <summary>
        /// Sends private message to target specified by id.
        /// </summary>
        /// <param name="targetClientId">Client id of the target.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        Task SendPrivateMessageAsync(Guid targetClientId, T message);

        /// <summary>
        /// Sends private message to target specified by id.
        /// </summary>
        /// <param name="targetClientId">Client id of the target.</param>
        /// <param name="message">Message to be sent.</param>
        void SendPrivateMessage(Guid targetClientId, T message);
    }
}
