using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class AdapterCollection
    {
        AdapterCollectionDispatcherBase dispatcher;
        object dispatcherLock = new object();

        public AdapterCollection()
        {
            dispatcher = AdapterCollectionDispatcherBase.Create();
        }

        public AdapterCollection(IRemoteHubAdapter<byte[]> adapter)
        {
            dispatcher = AdapterCollectionDispatcherBase.Create(adapter);
        }

        public bool Add(IRemoteHubAdapter<byte[]> adapter)
        {
            lock (dispatcherLock)
            {
                dispatcher = dispatcher.Add(adapter, out var result);
                return result;
            }
        }

        public int Count
        {
            get
            {
                lock (dispatcherLock)
                {
                    return dispatcher.Count;
                }
            }
        }

        public bool Remove(IRemoteHubAdapter<byte[]> adapter)
        {
            lock (dispatcherLock)
            {
                dispatcher = dispatcher.Remove(adapter, out var result);
                return result;
            }
        }

        public IRemoteHubAdapter<byte[]> GetOne()
        {
            return dispatcher.GetOne();
        }
    }
}
