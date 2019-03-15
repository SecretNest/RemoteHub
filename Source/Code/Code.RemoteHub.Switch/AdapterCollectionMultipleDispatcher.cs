using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class AdapterCollectionMultipleDispatcher : AdapterCollectionDispatcherBase
    {
        List<IRemoteHubAdapter<byte[]>> adapters = new List<IRemoteHubAdapter<byte[]>>();
        int adapterIndexMax = 1;
        int adapterIndex = 0;

        public AdapterCollectionMultipleDispatcher(IRemoteHubAdapter<byte[]> adapter1, IRemoteHubAdapter<byte[]> adapter2)
        {
            adapters.Add(adapter1);
            adapters.Add(adapter2);
        }

        public override int Count => adapters.Count;

        public override AdapterCollectionDispatcherBase Add(IRemoteHubAdapter<byte[]> adapter, out bool result)
        {
            lock (adapters)
            {
                result = !adapters.Contains(adapter);
                if (result)
                {
                    adapters.Add(adapter);
                    adapterIndexMax++;
                }
            }
            return this;
        }

        public override IRemoteHubAdapter<byte[]> GetOne()
        {
            lock (adapters)
            {
                var adapter = adapters[adapterIndex];

                if (adapterIndex == adapterIndexMax)
                    adapterIndex = 0;
                else
                    adapterIndex++;

                return adapter;
            }
        }

        public override AdapterCollectionDispatcherBase Remove(IRemoteHubAdapter<byte[]> adapter, out bool result)
        {
            lock (adapters)
            {
                int index = adapters.IndexOf(adapter);
                
                if (index == -1)
                {
                    result = false;
                    return this;
                }
                else
                {
                    result = true;

                    if (adapterIndexMax == 1)
                    {
                        return new AdapterCollectionOneDispatcher(adapters[1 - index]);
                    }
                    else
                    {
                        adapterIndexMax--;
                        adapters.RemoveAt(index);
                        if (adapterIndex > index)
                            adapterIndex--;
                        return this;
                    }
                }
            }
        }
    }
}
