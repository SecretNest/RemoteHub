using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class AdapterCollectionOneDispatcher : AdapterCollectionDispatcherBase
    {
        IRemoteHubAdapter<byte[]> adapter;
        public AdapterCollectionOneDispatcher()
        {
            adapter = null;
        }

        public AdapterCollectionOneDispatcher(IRemoteHubAdapter<byte[]> adapter)
        {
            this.adapter = adapter;
        }

        public override int Count
        {
            get
            {
                if (adapter == null)
                    return 0;
                else
                    return 1;
            }
        }

        public override AdapterCollectionDispatcherBase Add(IRemoteHubAdapter<byte[]> adapter, out bool result)
        {
            if (this.adapter == null)
            {
                this.adapter = adapter;
                result = true;
                return this;
            }
            else if (this.adapter == adapter)
            {
                result = false;
                return this;
            }
            else
            {
                result = true;
                return new AdapterCollectionMultipleDispatcher(this.adapter, adapter);
            }
        }

        public override IRemoteHubAdapter<byte[]> GetOne()
        {
            return adapter;
        }

        public override AdapterCollectionDispatcherBase Remove(IRemoteHubAdapter<byte[]> adapter, out bool result)
        {
            if (this.adapter == adapter)
            {
                adapter = null;
                result = true;
            }
            else
            {
                result = false;
            }
            return this;
        }
    }
}
