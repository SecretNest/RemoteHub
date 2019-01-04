using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    internal class ClientEntity
    {
        public string CommandTextRefresh { get; private set; } 
        public string CommandTextRefreshFull { get; private set; }

        string prefixForRefresh, prefixForRefreshFull;

        public ClientEntity(string prefixForRefresh, string prefixForRefreshFull)
        {
            this.prefixForRefresh = prefixForRefresh;
            this.prefixForRefreshFull = prefixForRefreshFull;
            CommandTextRefresh = prefixForRefresh;
            CommandTextRefreshFull = prefixForRefreshFull;
        }

        public void ApplyVirtualHostSetting(params KeyValuePair<Guid, VirtualHostSetting>[] settings)
        {
            if (settings == null || settings.Length == 0)
            {
                CommandTextRefresh = prefixForRefresh;
                CommandTextRefreshFull = prefixForRefresh;
            }
            else
            {
                string id = Guid.NewGuid().ToString("N");
                CommandTextRefresh = prefixForRefresh + id;
                CommandTextRefreshFull = prefixForRefreshFull + id + ":"
                     + string.Join(",", Array.ConvertAll(settings, i => string.Format("{0:N}-{1}-{2}", i.Key, i.Value.Priority, i.Value.Weight)));
            }
        }
    }
}
