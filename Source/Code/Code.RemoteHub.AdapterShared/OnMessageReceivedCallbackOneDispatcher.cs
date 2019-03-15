using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    class OnMessageReceivedCallbackOneDispatcher<T> : OnMessageReceivedCallbackDispatcherBase<T>
    {
        OnMessageReceivedCallback<T> callback;
        public OnMessageReceivedCallbackOneDispatcher(OnMessageReceivedCallback<T> callback)
        {
            this.callback = callback;
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> AddCallback(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null || callback == this.callback) return this;
            else return new OnMessageReceivedCallbackMultipleDispatcher<T>(new OnMessageReceivedCallback<T>[] { this.callback, callback });
        }

        public override void CallAndForget(Guid receiverClientId, T message)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() => callback(receiverClientId, message));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public override Task CallAsync(Guid receiverClientId, T message)
        {
            return Task.Run(() => callback(receiverClientId, message));
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveAllCallbacks()
        {
            return new OnMessageReceivedCallbackNoneDispatcher<T>();
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveCallback(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null && callback != this.callback) return this;
            else return new OnMessageReceivedCallbackNoneDispatcher<T>();
        }
    }
}
