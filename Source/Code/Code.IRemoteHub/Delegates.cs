using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents a method that will handle the message received from private channel.
    /// </summary>
    /// <param name="receiverClientId">Client id of the receiver.</param>
    /// <typeparam name="T">Type of the message data.</typeparam>
    /// <param name="message">Message body.</param>
    public delegate void OnMessageReceivedCallback<T>(Guid receiverClientId, T message);
}
