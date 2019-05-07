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
     *   
     * Note:
     *   RedisAdapter/Client is one component which connects the operating side and redis.
     *   StreamAdapter/Client needs to work in pair which are connected to each other by using stream(s). In one pair of StreamAdapter/Client, each one will be connected to one operating side, including Switch.
     */

    partial class Program
    {
        static Dictionary<Guid, string> clientNames = new Dictionary<Guid, string>();
        static Dictionary<IRemoteHubAdapter<byte[]>, string> adapterNamesForSwitch1 = new Dictionary<IRemoteHubAdapter<byte[]>, string>();
        static Dictionary<IRemoteHubAdapter<byte[]>, string> adapterNamesForSwitch2 = new Dictionary<IRemoteHubAdapter<byte[]>, string>();
        static List<IRemoteHub<string>> clients = new List<IRemoteHub<string>>();

        static void Main(string[] args)
        {
            //Redis part
            string redisConnectionString = "localhost"; //define redis connection string
            clients.Add(new RemoteHubOverRedis<string>(Guid.NewGuid(), redisConnectionString, Received)); //create one client to redis and add it to the clients list
            clients.Add(new RemoteHubOverRedis<string>(Guid.NewGuid(), redisConnectionString, Received)); //create one more client to redis and add it to the clients list
            clientNames.Add(clients[0].ClientId, "Client 0"); //name the 1st client as Client 0
            clientNames.Add(clients[1].ClientId, "Client 1"); //name the 2nd client as Client 1
            clients[0].Start(); //start the 1st client
            clients[1].Start(); //start the 2nd client
            RedisAdapter<byte[]> redisAdapterOnRedisHub = new RedisAdapter<byte[]>(redisConnectionString); //create an adapter connected to redis for later using in Switch 2

            //Switch1 part
            TcpListener[] tcpListeners = new TcpListener[] //open 3 tcp listeners
            {
                new TcpListener(IPAddress.Loopback, 60002),
                new TcpListener(IPAddress.Loopback, 60003),
                new TcpListener(IPAddress.Loopback, 60004)
            };
            foreach(var tcpListener in tcpListeners) //start tcp listeners
            {
                tcpListener.Start();
            }
            Task<TcpClient>[] acceptingTasks = Array.ConvertAll(tcpListeners, i => i.AcceptTcpClientAsync()); //waiting for connection for each server
            TcpClient[] tcpClients = new TcpClient[] //prepare 6 tcp links, connection relation: 0<->3, 1<->4, 2<->5
            {
                new TcpClient("localhost", 60002),
                new TcpClient("localhost", 60003),
                new TcpClient("localhost", 60004),
                acceptingTasks[0].Result,
                acceptingTasks[1].Result,
                acceptingTasks[2].Result
            };
            foreach (var tcpListener in tcpListeners) //stop all listeners
            {
                tcpListener.Stop();
            }
            NetworkStream[] streamsOfTcpClients= Array.ConvertAll(tcpClients, i => i.GetStream()); //get network streams from tcp links.
            StreamAdapter<byte[]>[] streamAdaptersOnSwitch1 = new StreamAdapter<byte[]>[] //create adapters from first 3 tcp links.
            {
                new StreamAdapter<byte[]>(streamsOfTcpClients[0], streamsOfTcpClients[0]),
                new StreamAdapter<byte[]>(streamsOfTcpClients[1], streamsOfTcpClients[1]),
                new StreamAdapter<byte[]>(streamsOfTcpClients[2], streamsOfTcpClients[2])
            };
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[0]] = "To Client 2"; //name the 1st adapter as To Client 2
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[1]] = "To Client 3"; //name the 2nd adapter as To Client 3
            adapterNamesForSwitch1[streamAdaptersOnSwitch1[2]] = "To Switch 2"; //name the 3rd adapter as To Switch 2 for later using in Switch 2
            clients.Add(new RemoteHubOverStream<string>(Guid.NewGuid(), streamsOfTcpClients[3], streamsOfTcpClients[3], Received)); //create one client based on the 3rd stream which is connected to the 1st stream adapter.
            clients.Add(new RemoteHubOverStream<string>(Guid.NewGuid(), streamsOfTcpClients[4], streamsOfTcpClients[4], Received)); //create one more client based on the 4th stream which is connected to the 2nd stream adapter.
            clientNames.Add(clients[2].ClientId, "Client 2"); //name the new created client as Client 2
            clientNames.Add(clients[3].ClientId, "Client 3"); //name the 2nd new created client as Client 3
            clients[2].Start(); //start the new created client
            clients[3].Start(); //start the 2nd new created client
            StreamAdapter<byte[]> streamAdapterOnSwitch2 = new StreamAdapter<byte[]>(streamsOfTcpClients[5], streamsOfTcpClients[5]); //create one adapter based on the 5th stream which is connected to the 3rd stream adapter.
            RemoteHubSwitch remoteHubSwitch1 = new RemoteHubSwitch(); //create the 1st Switch
            remoteHubSwitch1.RemoteClientAdded += RemoteHubSwitch1_RemoteClientAdded;
            remoteHubSwitch1.RemoteClientChanged += RemoteHubSwitch1_RemoteClientChanged;
            remoteHubSwitch1.RemoteClientRemoved += RemoteHubSwitch1_RemoteClientRemoved;
            //remoteHubSwitch1.MessageRouted += RemoteHubSwitch1_MessageRouted;
            //remoteHubSwitch1.MessageRoutingFailed += RemoteHubSwitch1_MessageRoutingFailed;
            remoteHubSwitch1.AddAdapters(streamAdaptersOnSwitch1); //add the new created adapter to the 1st Switch

            //Switch2 part
            adapterNamesForSwitch2[redisAdapterOnRedisHub] = "To Redis"; //name the redis adapter for switch 2 as To Redis
            adapterNamesForSwitch2[streamAdapterOnSwitch2] = "To Switch 1"; //name the stream adapter for switch 2 as To Switch 1
            RemoteHubSwitch remoteHubSwitch2 = new RemoteHubSwitch(); //create the 2nd Switch
            remoteHubSwitch2.RemoteClientAdded += RemoteHubSwitch2_RemoteClientAdded;
            remoteHubSwitch2.RemoteClientChanged += RemoteHubSwitch2_RemoteClientChanged;
            remoteHubSwitch2.RemoteClientRemoved += RemoteHubSwitch2_RemoteClientRemoved;
            //remoteHubSwitch2.MessageRouted += RemoteHubSwitch2_MessageRouted;
            //remoteHubSwitch2.MessageRoutingFailed += RemoteHubSwitch2_MessageRoutingFailed;
            remoteHubSwitch2.AddAdapter(redisAdapterOnRedisHub); //add the redis adapter to Switch 2
            remoteHubSwitch2.AddAdapter(streamAdapterOnSwitch2); //add the switch adapter to Switch 2

            //Test

            //SimpleMessageTest();
            //AddRemoveClientTest(remoteHubSwitch2);
            ConnectAndDisconnectSwitchTest(remoteHubSwitch1, streamAdaptersOnSwitch1[2]);

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey(true);

            //Dispose
            remoteHubSwitch1.RemoveAllAdapters(true); //remove all adapters attached in Switch 1
            remoteHubSwitch2.RemoveAllAdapters(true); //remove all adapters attached in Switch 2
            foreach (var client in clients) //stop all clients
            {
                client.Stop();
                ((IDisposable)client).Dispose();
            }
            redisAdapterOnRedisHub.Dispose();
            streamAdapterOnSwitch2.Dispose();
            foreach (var adapter in streamAdaptersOnSwitch1)
            {
                adapter.Dispose();
            }
            foreach (var stream in streamsOfTcpClients)
            {
                stream.Close();
                stream.Dispose();
            }
            foreach(var tcpClient in tcpClients)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }
    }
}
