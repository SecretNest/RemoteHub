using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;

namespace RedisLocalTest
{
    class Program
    {
        const string connectionString = "localhost";
        const string mainChannel = "Main";
        const string hostKeyPrefix = "TestClient_";
        const string privateChannelPrefix = "Private_";
        static Dictionary<Guid, string> clientNames = new Dictionary<Guid, string>();

        static void Main(string[] args)
        {
            Guid client1Id = Guid.NewGuid();
            Guid client2Id = Guid.NewGuid();
            Guid client3Id = Guid.NewGuid();
            RemoteHubOverRedis<string> client1 = new RemoteHubOverRedis<string>(client1Id, connectionString, Received);
            RemoteHubOverRedis<string> client2 = new RemoteHubOverRedis<string>(client2Id, connectionString, Received);
            RemoteHubOverRedis<string> client3 = new RemoteHubOverRedis<string>(client3Id, connectionString, Received);


            //Console.WriteLine(string.Format("ClientId: {0} {1} {2}", client1Id, client2Id, client3Id));
            clientNames.Add(client1Id, "Client1");
            clientNames.Add(client2Id, "Client2");
            clientNames.Add(client3Id, "Client3");

            client1.Start();
            client2.Start();
            client3.Start();

            Guid virtualHostId = Guid.NewGuid();
            client1.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 5)));
            client2.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 3)));
            client3.ApplyVirtualHosts(new KeyValuePair<Guid, VirtualHostSetting>(virtualHostId, new VirtualHostSetting(0, 2)));


            bool shouldContinue = true;
            while (shouldContinue)
            {
                Console.WriteLine(@"Press:
1: Send from client 1 to client 1
2: Send from client 1 to client 2
3: Send from client 1 to client 3
4: Send from client 2 to client 1
5: Send from client 2 to client 2
6: Send from client 2 to client 3
7: Send from client 3 to client 1
8: Send from client 3 to client 2
9: Send from client 3 to client 3
0: Send from client 1 to virtual host (1:50%/2:30%/3:20%)
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
                        client1.SendMessage(client3Id, "From 1 to 3");
                        break;
                    case ConsoleKey.D4:
                        client2.SendMessage(client1Id, "From 2 to 1");
                        break;
                    case ConsoleKey.D5:
                        client2.SendMessage(client2Id, "From 2 to 2");
                        break;
                    case ConsoleKey.D6:
                        client2.SendMessage(client3Id, "From 2 to 3");
                        break;
                    case ConsoleKey.D7:
                        client3.SendMessage(client1Id, "From 3 to 1");
                        break;
                    case ConsoleKey.D8:
                        client3.SendMessage(client2Id, "From 3 to 2");
                        break;
                    case ConsoleKey.D9:
                        client3.SendMessage(client3Id, "From 3 to 3");
                        break;
                    case ConsoleKey.D0:
                        if (client1.TryResolveVirtualHost(virtualHostId, out var hostId))
                        {
                            client1.SendMessage(hostId, "From 1 to virtual host (1:50%/2:30%/3:20%)");
                        }
                        break;
                    default:
                        shouldContinue = false;
                        break;
                }
            }

            client1.Stop();
            client2.Stop();
            client3.Stop();

            Console.WriteLine("Done. Press any key to quit...");
            Console.ReadKey(true);
        }

        static void Received(Guid clientId, string text)
        {
            Console.WriteLine(string.Format("Received: {0}: {1}", clientNames[clientId], text));
        }
    }
}
