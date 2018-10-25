using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    public delegate void OnMessageReceivedCallback<T>(Guid clientId, T message);

}
