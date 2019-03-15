using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    class PrivateMessageCallbackHelper<T>
    {
        OnMessageReceivedCallbackDispatcherBase<T> dispatcher;
        object callbackLock = new object();
        public PrivateMessageCallbackHelper(OnMessageReceivedCallback<T> initializingCallback)
        {
            dispatcher = OnMessageReceivedCallbackDispatcherBase<T>.Create(initializingCallback);
        }

        public void AddCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (callbackLock)
            {
                dispatcher = dispatcher.AddCallback(callback);
            }
        }

        public void RemoveCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (callbackLock)
            {
                dispatcher = dispatcher.RemoveCallback(callback);
            }
        }

        public void RemoveAllCallbacks()
        {
            dispatcher = dispatcher.RemoveAllCallbacks();
        }

        public Task CallAsync(Guid receiverClientId, T message)
        {
            return dispatcher.CallAsync(receiverClientId, message);
        }

        public void CallAndForget(Guid receiverClientId, T message)
        {
            dispatcher.CallAndForget(receiverClientId, message);
        }
    }
}
