using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SwitchTest
{
    /* Clients:
     *   0: Connected to Redis
     *   1: Connected to Redis
     *   2: Connected to streamAdaptersOnSwitch1<byte[]>[0] on Switch 1
     *   3: Connected to streamAdaptersOnSwitch1<byte[]>[1] on Switch 1
     * 
     * Redis:
     *   clients[0]
     *   clients[1]
     *   redisAdapterOnRedisHub<byte[]> connected to Switch2
     * 
     * Switch1:
     *   streamAdaptersOnSwitch1<byte[]>[0] connected to clients[2]
     *   streamAdaptersOnSwitch1<byte[]>[1] connected to clients[3]
     *   streamAdaptersOnSwitch1<byte[]>[2] connected to streamAdapterOnSwitch2<byte[]> on Switch2
     *   
     * Switch2:
     *   redisAdapterOnRedisHub<byte[]> on Redis
     *   streamAdapterOnSwitch2<byte[]> connected to streamAdaptersOnSwitch1<byte[]>[2] on Switch1
     */

    class Program
    {
        static Dictionary<Guid, string> clientNames = new Dictionary<Guid, string>();
        static Dictionary<IRemoteHubAdapter<byte[]>, string> adapterNamesForSwitch1 = new Dictionary<IRemoteHubAdapter<byte[]>, string>();
        static Dictionary<IRemoteHubAdapter<byte[]>, string> adapterNamesForSwitch2 = new Dictionary<IRemoteHubAdapter<byte[]>, string>();

        static void Received(Guid clientId, string text)
        {
            Console.WriteLine(string.Format("Received: {0}: {1}", clientNames[clientId], text));
        }

        static void Main(string[] args)
        {
            List<IRemoteHub<string>> clients = new List<IRemoteHub<string>>();

            //Redis part
            string redisConnectionString = "localhost";
            clients.Add(new RemoteHubOverRedis<string>(Guid.NewGuid(), redisConnectionString, Received));
            clients.Add(new RemoteHubOverRedis<string>(Guid.NewGuid(), redisConnectionString, Received));
            clientNames.Add(clients[0].ClientId, "Client 0");
            clientNames.Add(clients[1].ClientId, "Client 1");
            clients[0].Start();
            clients[1].Start();
            RedisAdapter<byte[]> redisAdapterOnRedisHub = new RedisAdapter<byte[]>(redisConnectionString);

            //Switch1 part
            TcpListener[] tcpListeners = new TcpListener[]
            {
                new TcpListener(IPAddress.Loopback, 60002),
                new TcpListener(IPAddress.Loopback, 60003),
                new TcpListener(IPAddress.Loopback, 60004)
            };
            foreach(var tcpListener in tcpListeners)
            {
                tcpListener.Start();
            }
            Task<TcpClient>[] acceptingTasks = Array.ConvertAll(tcpListeners, i => i.AcceptTcpClientAsync());
            TcpClient[] tcpClients = new TcpClient[] 
            {
                new TcpClient("localhost", 60002),
                new TcpClient("localhost", 60003),
                new TcpClient("localhost", 60004),
                acceptingTasks[0].Result,
                acceptingTasks[1].Result,
                acceptingTasks[2].Result
            };
            foreach (var tcpListener in tcpListeners)
            {
                tcpListener.Stop();
            }
            NetworkStream[] streamsOfTcpClients= Array.ConvertAll(tcpClients, i => i.GetStream());
            StreamAdapter<byte[]>[] streamAdaptersOnSwitch1 = new StreamAdapter<byte[]>[]
            {
                new StreamAdapter<byte[]>(streamsOfTcpClients[0], streamsOfTcpClients[0]),
                new StreamAdapter<byte[]>(streamsOfTcpClients[1], streamsOfTcpClients[1]),
                new StreamAdapter<byte[]>(streamsOfTcpClients[2], streamsOfTcpClients[2])
            };
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[0]] = "To Client 2";
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[1]] = "To Client 3";
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[2]] = "To Switch 2";
            clients.Add(new RemoteHubOverStream<string>(Guid.NewGuid(), streamsOfTcpClients[3], streamsOfTcpClients[3], Received));
            clients.Add(new RemoteHubOverStream<string>(Guid.NewGuid(), streamsOfTcpClients[4], streamsOfTcpClients[4], Received));
            clientNames.Add(clients[2].ClientId, "Client 2");
            clientNames.Add(clients[3].ClientId, "Client 3");
            clients[2].Start();
            clients[3].Start();
            StreamAdapter<byte[]> streamAdapterOnSwitch2 = new StreamAdapter<byte[]>(streamsOfTcpClients[5], streamsOfTcpClients[5]);
            RemoteHubSwitch remoteHubSwitch1 = new RemoteHubSwitch();
            remoteHubSwitch1.RemoteClientAdded += RemoteHubSwitch1_RemoteClientAdded;
            remoteHubSwitch1.RemoteClientChanged += RemoteHubSwitch1_RemoteClientChanged;
            remoteHubSwitch1.RemoteClientRemoved += RemoteHubSwitch1_RemoteClientRemoved;
            remoteHubSwitch1.MessageRouted += RemoteHubSwitch1_MessageRouted;
            remoteHubSwitch1.MessageRoutingFailed += RemoteHubSwitch1_MessageRoutingFailed;
            remoteHubSwitch1.AddAdapters(streamAdaptersOnSwitch1);

            //Switch2 part
            adapterNamesForSwitch2[redisAdapterOnRedisHub] = "To Redis";
            adapterNamesForSwitch2[streamAdapterOnSwitch2] = "To Switch 1";
            RemoteHubSwitch remoteHubSwitch2 = new RemoteHubSwitch();
            remoteHubSwitch2.RemoteClientAdded += RemoteHubSwitch2_RemoteClientAdded;
            remoteHubSwitch2.RemoteClientChanged += RemoteHubSwitch2_RemoteClientChanged;
            remoteHubSwitch2.RemoteClientRemoved += RemoteHubSwitch2_RemoteClientRemoved;
            remoteHubSwitch2.MessageRouted += RemoteHubSwitch2_MessageRouted;
            remoteHubSwitch2.MessageRoutingFailed += RemoteHubSwitch2_MessageRoutingFailed;
            remoteHubSwitch2.AddAdapter(redisAdapterOnRedisHub);
            remoteHubSwitch2.AddAdapter(streamAdapterOnSwitch2);

            //Test
            while (true)
            {
                Console.WriteLine("From: 0/1/2/3 other to quit...");
                if (!TryGetClientIndex(out int sourceIndex))
                {
                    break;
                }
                var client = clients[sourceIndex];
                Console.WriteLine("To: 0/1/2/3 other to quit...");
                if (!TryGetClientIndex(out int targetIndex))
                {
                    break;
                }
                var target = clients[targetIndex].ClientId; //Get Id only. Not related to any operating on target client.
                Console.WriteLine("From: {0} To: {1}", sourceIndex, targetIndex);
                client.SendMessage(target, "Test Message");
            }

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey(true);


            /* Test to do:
             * adding / removing client
             * connect / disconnect switch links
             */


            //Dispose
            remoteHubSwitch1.RemoveAllAdapters(true);
            remoteHubSwitch2.RemoveAllAdapters(true);
            foreach (var client in clients)
            {
                client.Stop();
            }
            foreach(var stream in streamsOfTcpClients)
            {
                stream.Dispose();
            }
            foreach(var tcpClient in tcpClients)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }
            streamAdapterOnSwitch2.Dispose();
            foreach (var adapter in streamAdaptersOnSwitch1)
            {
                adapter.Dispose();
            }
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

        static bool TryGetClientIndex(out int index)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.D0 || key.Key == ConsoleKey.NumPad0)
            {
                index = 0;
                return true;
            }
            else if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
            {
                index = 1;
                return true;
            }
            else if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
            {
                index = 2;
                return true;
            }
            else if (key.Key == ConsoleKey.D3 || key.Key == ConsoleKey.NumPad3)
            {
                index = 3;
                return true;
            }
            else
            {
                index = -1;
                return false;
            }
        }
    }
}
