using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;

namespace SwitchTest
{
    partial class Program
    {
        static void VirtualHostTest(RemoteHubSwitch remoteHubSwitch1, StreamAdapter<byte[]> streamAdaptersOnSwitch1, IRemoteHub<string> sender)
        {
            sender.RemoteClientUpdated += Sender_RemoteClientUpdated;

            //reg
            Console.WriteLine("Please wait for several seconds and press any key to reg virtual hosts...");
            Console.ReadKey(true);

            Guid virtualHostId = Guid.NewGuid();
            foreach(var client in clients)
            {
                client.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 1)));
            }
            //clients[0].ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(1, 1))); //this command will add higher setting on only clients[0] which will suppress all other clients on the same virtual host.

            Console.WriteLine("Please wait for several seconds and press any key to continue sending test...");
            Console.ReadKey(true);

            //send
            for (int i = 0; i < 100; i++)
            {
                if (sender.TryResolveVirtualHost(virtualHostId, out var hostId))
                {
                    string testMessage = string.Format("<-- {0:D2}: To Virtual Host on {1} -->", i, clientNames[hostId]);
                    waitingTexts.Add(testMessage);
                    sender.SendMessage(hostId, testMessage);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            if (waitingTexts.Count > 0)
            {
                foreach (var text in waitingTexts)
                {
                    Console.WriteLine("Missing message: " + text);
                }
                waitingTexts.Clear();
            }
            else
            {
                Console.WriteLine("All message received.");
            }

            //disconnect switch
            Console.WriteLine("Disconnecting...");
            remoteHubSwitch1.RemoveAdapter(streamAdaptersOnSwitch1);

            //add switch direct link
            Console.WriteLine("Adding client...");
            RemoteHubSwitchDirect<string> clientDirect = new RemoteHubSwitchDirect<string>(Guid.NewGuid(), Received);
            adapterNamesForSwitch1[clientDirect] = "To SwitchDirect"; //name the new created adapter as To SwitchDirect
            clients.Add(clientDirect);
            clientNames.Add(clientDirect.ClientId, "SwitchDirect"); //name the client as SwitchDirect
            remoteHubSwitch1.AddAdapter(clientDirect);

            //set another virtual host
            clientDirect.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 3)));
            Console.WriteLine("Please wait a while for client (SwitchDirect) discovery and press any key to continue...");

            //resume switch connection
            Console.WriteLine("Reconnecting...");
            remoteHubSwitch1.AddAdapter(streamAdaptersOnSwitch1);

            Console.ReadKey(true);
            //send
            for (int i = 0; i < 100; i++)
            {
                if (sender.TryResolveVirtualHost(virtualHostId, out var hostId))
                {
                    string testMessage = string.Format("<-- {0:D2}: To Virtual Host on {1} -->", i, clientNames[hostId]);
                    waitingTexts.Add(testMessage);
                    sender.SendMessage(hostId, testMessage);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);

            if (waitingTexts.Count > 0)
            {
                foreach (var text in waitingTexts)
                {
                    Console.WriteLine("Missing message: " + text);
                }
                waitingTexts.Clear();
            }
            else
            {
                Console.WriteLine("All message received.");
            }
        }

        private static void Sender_RemoteClientUpdated(object sender, ClientWithVirtualHostSettingEventArgs e)
        {
            Console.WriteLine("Sender found RemoteClientUpdated: {0}, {1}", clientNames[e.ClientId], e.VirtualHostSettingId);
        }
    }
}
