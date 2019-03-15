using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    class OnMessageReceivedCallbackNoneDispatcher<T> : OnMessageReceivedCallbackDispatcherBase<T>
    {
        public override OnMessageReceivedCallbackDispatcherBase<T> AddCallback(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null) return this;
            else return new OnMessageReceivedCallbackOneDispatcher<T>(callback);
        }

        public override void CallAndForget(Guid receiverClientId, T message)
        {
            return;
        }

        public override Task CallAsync(Guid receiverClientId, T message)
        {
            return Task.CompletedTask;
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveAllCallbacks()
        {
            return this;
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveCallback(OnMessageReceivedCallback<T> callback)
        {
            return this;
        }
    }
}
