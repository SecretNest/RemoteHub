using System;
using SecretNest.RemoteAgency.Attributes;

namespace SharedInterface
{
    public interface IChatServer
    {
        [CustomizedOneWayOperating] //dont need the response from the remote site.
        [EventParameterIgnored("sender")] //dont send "sender" argument defined in EventHandler to the remote site.
        event EventHandler<MessageReceivedEventArgs> MessageReceived; //When a message received.

        [CustomizedOneWayOperating] //dont need the response from the remote site.
        void SendMessage(string name, string text); //Send a message.
    }
}
