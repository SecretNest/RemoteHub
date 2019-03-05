using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    class AdatperPackage
    {
        IRemoteHubAdapter remoteHubAdapter;
        public IRemoteHubAdapter RemoteHubAdapter => remoteHubAdapter;

        public AdatperPackage(IRemoteHubAdapter remoteHubAdapter)
        {
            this.remoteHubAdapter = remoteHubAdapter;

        }
    }
}
