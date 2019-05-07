using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SwitchTest
{
    partial class Program
    {
        static void AddRemoveClientTest(RemoteHubSwitch remoteHubSwitch2)
        {
            //adding a new client to Redis
            string redisConnectionString = "localhost"; //define redis connection string
            clients.Add(new RemoteHubOverRedis<string>(Guid.NewGuid(), redisConnectionString, Received)); //create one new client to redis and add it to the clients list
            clientNames.Add(clients[4].ClientId, "New Redis Client"); //name the client as New Redis Client
            clients[4].Start(); //start the new created client

            //adding a new client and a connected adapter in pair to Switch 2
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 60005); //open one tcp listener
            tcpListener.Start(); //start tcp listener
            Task<TcpClient> acceptingTask = tcpListener.AcceptTcpClientAsync(); //waiting for connection
            TcpClient[] tcpClients = new TcpClient[] //prepare 2 tcp links, connected in pair
            {
                new TcpClient("localhost", 60005),
                acceptingTask.Result
            };
            tcpListener.Stop(); //stop listener
            NetworkStream[] streamsOfTcpClients = Array.ConvertAll(tcpClients, i => i.GetStream()); //get network streams from tcp links.
            StreamAdapter<byte[]> streamAdapterOnSwitch2 = new StreamAdapter<byte[]>(streamsOfTcpClients[0], streamsOfTcpClients[0]); //create adapter from one stream
            adapterNamesForSwitch2[streamAdapterOnSwitch2] = "To New Stream Client"; //name the new created adapter as To New Stream Client
            clients.Add(new RemoteHubOverStream<string>(Guid.NewGuid(), streamsOfTcpClients[1], streamsOfTcpClients[1], Received)); //create one client based on the other stream which is connected to the stream adapter.
            clientNames.Add(clients[5].ClientId, "New Stream Client"); //name the client as New Stream Client
            clients[5].Start(); //start the new created client
            remoteHubSwitch2.AddAdapter(streamAdapterOnSwitch2); //add the switch adapter to Switch 2
            Console.WriteLine("Please wait a while for clients (New Stream Client & New Redis Client) discovery.");

            //Sending test messages
            Task one = Task.Run(async () => await SendTestMessages());
            one.Wait();

            //removing the new added clients
            remoteHubSwitch2.RemoveAdapter(streamAdapterOnSwitch2);
            streamAdapterOnSwitch2.Stop();
            streamAdapterOnSwitch2.Dispose();
            adapterNamesForSwitch2.Remove(streamAdapterOnSwitch2);
            clients[5].Stop();
            ((IDisposable)clients[5]).Dispose();
            clients.RemoveAt(5);
            clients[4].Stop();
            ((IDisposable)clients[4]).Dispose();
            clients.RemoveAt(4);
            foreach (var stream in streamsOfTcpClients)
            {
                stream.Close();
                stream.Dispose();
            }
            foreach (var tcpClient in tcpClients)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }

            Console.WriteLine("Please wait a while for clients (New Stream Client & New Redis Client) removal.");

            //Sending test messages
            Task two = Task.Run(async () => await SendTestMessages());
            two.Wait();
        }

        static async Task SendTestMessages()
        {
            Console.WriteLine("Press any key to sending messages...");
            Console.ReadKey(true);
            foreach (var source in clients.ToArray())
            {
                var sourceName = clientNames[source.ClientId];

                foreach(var target in clients.ToArray())
                {
                    var targetClientId = target.ClientId; //Get Id only. Not related to any operating on target client.
                    var targetName = clientNames[targetClientId];
                    
                    string testMessage = string.Format("<-- From {0} to {1} -->", sourceName, targetName);
                    waitingTexts.Add(testMessage);
                    //Console.WriteLine("Sending message from {0} to {1}...", sourceName, targetName);

                    await source.SendMessageAsync(targetClientId, testMessage);
                }
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            if (waitingTexts.Count > 0)
            {
                foreach(var text in waitingTexts)
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
    }
}
