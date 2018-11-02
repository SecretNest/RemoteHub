using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;

namespace SecretNest.RemoteHub
{
    public class MultiHub<T>
    {
        ConcurrentDictionary<Guid, IRemoteHub<T>> remoteHubs = new ConcurrentDictionary<Guid, IRemoteHub<T>>();

        public void AddHub(IRemoteHub<T> remoteHub)
        {
            var existed = remoteHubs.GetOrAdd(remoteHub.ClientId, remoteHub);
            if (existed != remoteHub)
            {
                throw new ArgumentException("RemoteHub with same client id exists.", nameof(remoteHub));
            }

            HubAdded(remoteHub);
        }

        public bool RemoveHub(Guid remoteHubClientId)
        {
            if (remoteHubs.TryRemove(remoteHubClientId, out var removed))
            {
                HubRemoved(removed);
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public IReadOnlyList<IRemoteHub<T>> RemoteHubs
        {
            get
            {
                return remoteHubs.Values.ToArray();
            }
        }

        





        void HubAdded(IRemoteHub<T> remoteHub)
        {

        }

        void HubRemoved(IRemoteHub<T> remoteHub)
        {

        }

    }
}
