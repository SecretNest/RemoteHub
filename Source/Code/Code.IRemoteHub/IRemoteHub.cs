using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents the base, non-generic version of the generic <see cref="IRemoteHub{T}"/>.
    /// </summary>
    public interface IRemoteHub
    {
        /// <summary>
        /// Occurs while an connection related exception is thrown.
        /// </summary>
        event EventHandler<ConnectionExceptionEventArgs> ConnectionErrorOccurred;

        /// <summary>
        /// Gets the client id.
        /// </summary>
        Guid ClientId { get; }

        /// <summary>
        /// Applies the virtual hosts setting for current client.
        /// </summary>
        /// <param name="settings">Virtual host settings. Key is virtual host id; Value is setting related to the virtual host specified.</param>
        void ApplyVirtualHosts(params KeyValuePair<Guid, VirtualHostSetting>[] settings);

        /// <summary>
        /// Tries to resolve virtual host by id to host id.
        /// </summary>
        /// <param name="virtualHostId">Virtual host id.</param>
        /// <param name="hostId">Host id.</param>
        /// <returns>Whether the resolving is succeeded or not.</returns>
        bool TryResolveVirtualHost(Guid virtualHostId, out Guid hostId);


        /// <summary>
        /// Starts instance operating.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops instance operating.
        /// </summary>
        void Stop();
    }




    /// <summary>
    /// Represents the methods, properties and event of RemoteHub.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    public interface IRemoteHub<T> : IRemoteHub
    {
        /// <summary>
        /// Sends a message to the target host specified by id.
        /// </summary>
        /// <param name="targetHostId">Target host id.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>Whether the resolving from target host id is succeeded or not.</returns>
        bool SendMessage(Guid targetHostId, T message);

        /// <summary>
        /// Creates a task that sends a message to the target host specified by id.
        /// </summary>
        /// <param name="targetHostId"></param>
        /// <param name="message"></param>
        /// <returns>A task that represents the sending job.</returns>
        Task<bool> SendMessageAsync(Guid targetHostId, T message);

        /// <summary>
        /// Sends a message to the target host specified by private channel name.
        /// </summary>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        void SendMessage(string targetChannel, T message);

        /// <summary>
        /// Creates a task that sends a message to the target host specified by private channel name.
        /// </summary>
        /// <param name="targetChannel">Name of the private channel of the target host.</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>A task that represents the sending job.</returns>
        Task SendMessageAsync(string targetChannel, T message);
    }
}
