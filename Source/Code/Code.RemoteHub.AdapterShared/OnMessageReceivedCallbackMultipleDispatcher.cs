using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    class OnMessageReceivedCallbackMultipleDispatcher<T> : OnMessageReceivedCallbackDispatcherBase<T>
    {
        OnMessageReceivedCallback<T>[] callbacks;

        public OnMessageReceivedCallbackMultipleDispatcher(OnMessageReceivedCallback<T>[] callbacks)
        {
            this.callbacks = callbacks;
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> AddCallback(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null) return this;

            var index = Array.IndexOf(callbacks, callback);
            if (index == -1) return this;

            int length = callbacks.Length;

            var combined = new OnMessageReceivedCallback<T>[length + 1];
            Array.Copy(callbacks, combined, length);
            return new OnMessageReceivedCallbackMultipleDispatcher<T>(combined);
        }

        public override void CallAndForget(Guid receiverClientId, T message)
        {
            Parallel.ForEach(callbacks, i => i(receiverClientId, message));
        }

        public override Task CallAsync(Guid receiverClientId, T message)
        {
            return Task.WhenAll(Array.ConvertAll(callbacks, i=> Task.Run(() => i(receiverClientId, message))));
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveAllCallbacks()
        {
            return new OnMessageReceivedCallbackNoneDispatcher<T>();
        }

        public override OnMessageReceivedCallbackDispatcherBase<T> RemoveCallback(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null) return this;

            var index = Array.IndexOf(callbacks, callback);
            if (index == -1) return this;

            int length = callbacks.Length;

            if (length == 2)
                return new OnMessageReceivedCallbackOneDispatcher<T>(callbacks[1 - index]);

            var removed = new OnMessageReceivedCallback<T>[length - 1];

            if (index != 0)
            {
                Array.Copy(callbacks, removed, index);
            }
            if (index != length - 1)
            {
                Array.Copy(callbacks, index + 1, removed, index, length - 1 - index);
            }
            return new OnMessageReceivedCallbackMultipleDispatcher<T>(removed);
        }
    }
}
