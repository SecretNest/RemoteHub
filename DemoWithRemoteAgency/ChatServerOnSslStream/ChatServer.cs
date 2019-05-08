using SharedInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServerOnSslStream
{
    class ChatServer : IChatServer
    {
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public void SendMessage(string name, string text)
        {
            //When a message is received, raise event to broadcast it.
            MessageReceived?.Invoke(null, new MessageReceivedEventArgs(name, text));
        }
    }
}
