using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace Code.RemoteHub.Switch
{
    public class RemoteHubSwitch
    {
        Dictionary<IRemoteHubAdapter, AdatperPackage> adapterPackages = new Dictionary<IRemoteHubAdapter, AdatperPackage>();
        Dictionary<Guid, List<AdatperPackage>> adapterOfClients = new Dictionary<Guid, List<AdatperPackage>>();

        public IEnumerable<Guid> GetAllClients()
        {
            throw new Exception();
        }

        public void Start()
        {

        }

        public void Stop()
        {

        }

        public void AddAdapter(IRemoteHubAdapter adapter)
        {

        }

        public void AddAdapters(params IRemoteHubAdapter[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
                foreach (var adapter in adapters)
                    AddAdapter(adapter);
        }

        public void RemoveAdapter(IRemoteHubAdapter adapter)
        {

        }

        public void RemoveAdapters(params IRemoteHubAdapter[] adapters)
        {
            if (adapters != null && adapters.Length > 0)
                foreach (var adapter in adapters)
                    RemoveAdapter(adapter);
        }

        public void RemoveAllAdapters()
        {

        }
    }
}
