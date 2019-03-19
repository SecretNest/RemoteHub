using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StreamLocalTest
{
    class Program
    {
        static Dictionary<Guid, string> clientNames = new Dictionary<Guid, string>();

        static void Main(string[] args)
        {
            Guid client1Id = Guid.NewGuid();
            Guid client2Id = Guid.NewGuid();
            clientNames.Add(client1Id, "Client1");
            clientNames.Add(client2Id, "Client2");

            var servicePort = new IPEndPoint(IPAddress.Loopback, 60001);
            TcpListener server = new TcpListener(servicePort);
            server.Start();
            var acceptConnecting = server.AcceptTcpClientAsync();

            using (TcpClient tcpClient2 = new TcpClient("localhost", 60001))
            using (TcpClient tcpClient1 = acceptConnecting.Result)
            using (var stream1 = tcpClient1.GetStream())
            using (var stream2 = tcpClient2.GetStream())
            {
                server.Stop();

                RemoteHubOverStream<string> client1 = new RemoteHubOverStream<string>(client1Id, stream1, stream1, Received);
                RemoteHubOverStream<string> client2 = new RemoteHubOverStream<string>(client2Id, stream2, stream2, Received);

                client1.Start();
                client2.Start();

                Guid virtualHostId = Guid.NewGuid();
                client1.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 2)));
                client2.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 1)));


                bool shouldContinue = true;
                while (shouldContinue)
                {
                    Console.WriteLine(@"Press:
1: Send from client 1 to client 1
2: Send from client 1 to client 2
3: Send from client 2 to client 1
4: Send from client 2 to client 2
0: Send from client 1 to virtual host (1:67%/2:34%)
Other: Shutdown.");

                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1:
                            client1.SendMessage(client1Id, "From 1 to 1");
                            break;
                        case ConsoleKey.D2:
                            client1.SendMessage(client2Id, "From 1 to 2");
                            break;
                        case ConsoleKey.D3:
                            client2.SendMessage(client1Id, "From 2 to 1");
                            break;
                        case ConsoleKey.D4:
                            client2.SendMessage(client2Id, "From 2 to 2");
                            break;
                        case ConsoleKey.D0:
                            if (client1.TryResolveVirtualHost(virtualHostId, out var hostId))
                            {
                                client1.SendMessage(hostId, "From 1 to virtual host (1:67%/2:34%)");
                            }
                            break;
                        default:
                            shouldContinue = false;
                            break;
                    }
                }

                client1.Stop();
                client2.Stop();

                Console.WriteLine("Done. Press any key to quit...");
                Console.ReadKey(true);
            }

        }


        static void Received(Guid clientId, string text)
        {
            Console.WriteLine(string.Format("Received: {0}: {1}", clientNames[clientId], text));
        }
    }
}
