using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace SwitchTest
{
    partial class Program
    {
        static HashSet<string> waitingTexts = new HashSet<string>();

        static void Received(Guid clientId, string text)
        {
            Console.WriteLine(string.Format("Received: {0}: {1}", clientNames[clientId], text));
            waitingTexts.Remove(text);
        }

        private static void RemoteHubSwitch1_MessageRoutingFailed(object sender, MessageRoutingFailedEventArgs e) => WriteLog("Switch1 Message Routing Failed.", e);

        private static void RemoteHubSwitch1_MessageRouted(object sender, MessageRoutedEventArgs e) => WriteLog("Switch1 Message Routed.", e, adapterNamesForSwitch1);

        private static void RemoteHubSwitch2_MessageRoutingFailed(object sender, MessageRoutingFailedEventArgs e) => WriteLog("Switch2 Message Routing Failed.", e);

        private static void RemoteHubSwitch2_MessageRouted(object sender, MessageRoutedEventArgs e) => WriteLog("Switch2 Message Routed.", e, adapterNamesForSwitch2);

        private static void RemoteHubSwitch1_RemoteClientRemoved(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch1 Client Removed.", e, adapterNamesForSwitch1);

        private static void RemoteHubSwitch2_RemoteClientRemoved(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch2 Client Removed.", e, adapterNamesForSwitch2);

        private static void RemoteHubSwitch1_RemoteClientAdded(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch1 Client Added.", e, adapterNamesForSwitch1);

        private static void RemoteHubSwitch2_RemoteClientAdded(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch2 Client Added.", e, adapterNamesForSwitch2);

        private static void RemoteHubSwitch1_RemoteClientChanged(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch1 Client Changed.", e, adapterNamesForSwitch1);

        private static void RemoteHubSwitch2_RemoteClientChanged(object sender, RemoteClientChangedEventArgs e) => WriteLog("Switch2 Client Changed.", e, adapterNamesForSwitch2);


        static void WriteLog(string name, ClientIdEventArgs e)
        {
            Console.WriteLine("{0} ClientId: {1}", name, clientNames[e.ClientId]);
        }

        static void WriteLog(string name, IGetRelatedRemoteHubAdapterInstance e, Dictionary<IRemoteHubAdapter<byte[]>, string> adapterNames)
        {
            Console.WriteLine("{0} ClientId: {1}; Adapter: {2}", name, clientNames[((ClientIdEventArgs)e).ClientId], adapterNames[e.Adapter]);
        }
    }
}
