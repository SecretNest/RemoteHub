using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SwitchTest
{
    partial class Program
    {
        static void ConnectAndDisconnectSwitchTest(RemoteHubSwitch remoteHubSwitch1, StreamAdapter<byte[]> streamAdaptersOnSwitch1)
        {
            //Sending test messages
            Task sending = Task.Run(async () => await SendTestMessages());
            sending.Wait();

            //disconnect switches
            Console.WriteLine("Disconnecting...");
            remoteHubSwitch1.RemoveAdapter(streamAdaptersOnSwitch1);

            //Sending test messages
            Console.WriteLine("There should be some messages missing due to disconnection.");
            sending = Task.Run(async () => await SendTestMessages());
            sending.Wait();

            //reconnect switches
            Console.WriteLine("Reconnecting...");
            remoteHubSwitch1.AddAdapter(streamAdaptersOnSwitch1);

            //Sending test messages
            sending = Task.Run(async () => await SendTestMessages());
            sending.Wait();
        }
    }
}
