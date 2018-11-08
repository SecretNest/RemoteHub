using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents a method that will handle the message received from private channel.
    /// </summary>
    /// <typeparam name="T">Type of the message data.</typeparam>
    /// <param name="id">Id of the identification of this RemoteHub instance used in MultiHub.</param>
    /// <param name="clientId">Receiver id.</param>
    /// <param name="message">Message body.</param>
    public delegate void OnMessageReceivedFromMultiHubCallback<T>(Guid id, Guid clientId, T message);
}
