using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    abstract class OnMessageReceivedCallbackDispatcherBase<T>
    {
        public static OnMessageReceivedCallbackDispatcherBase<T> Create(OnMessageReceivedCallback<T> callback)
        {
            if (callback == null) return new OnMessageReceivedCallbackNoneDispatcher<T>();
            else return new OnMessageReceivedCallbackOneDispatcher<T>(callback);
        }

        public abstract OnMessageReceivedCallbackDispatcherBase<T> AddCallback(OnMessageReceivedCallback<T> callback);

        public abstract OnMessageReceivedCallbackDispatcherBase<T> RemoveCallback(OnMessageReceivedCallback<T> callback);

        public abstract OnMessageReceivedCallbackDispatcherBase<T> RemoveAllCallbacks();

        public abstract Task CallAsync(Guid receiverClientId, T message);

        public abstract void CallAndForget(Guid receiverClientId, T message);
    }
}
