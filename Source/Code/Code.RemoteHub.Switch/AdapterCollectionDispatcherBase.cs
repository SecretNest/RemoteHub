using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    abstract class AdapterCollectionDispatcherBase
    {
        public abstract AdapterCollectionDispatcherBase Add(IRemoteHubAdapter<byte[]> adapter, out bool result);

        public abstract int Count { get; }

        public abstract AdapterCollectionDispatcherBase Remove(IRemoteHubAdapter<byte[]> adapter, out bool result);

        public abstract IRemoteHubAdapter<byte[]> GetOne();

        public static AdapterCollectionDispatcherBase Create()
        {
            return new AdapterCollectionOneDispatcher();
        }

        public static AdapterCollectionDispatcherBase Create(IRemoteHubAdapter<byte[]> adapter)
        {
            return new AdapterCollectionOneDispatcher(adapter);
        }
    }
}
