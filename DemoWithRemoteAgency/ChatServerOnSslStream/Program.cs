using SecretNest.RemoteAgency;
using SecretNest.RemoteHub;
using SharedInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServerOnSslStream
{
    class Program
    {
        static readonly IPAddress serverIP = IPAddress.Loopback;
        static readonly int serverPort = 34567;

        static CancellationTokenSource shutdownSignal = new CancellationTokenSource();

        static TcpListener tcpListener;

        static RemoteHubSwitch remoteHubSwitch;
        static RemoteHubSwitchDirect<string> localClient;
        static RemoteAgencyManagerEncapsulated remoteAgency;

        static List<ClientEntity> clients = new List<ClientEntity>();

        static void Main(string[] args)
        {
            //Create chat server instance
            var chatServer = new ChatServer();

            //Remote Hub Switch
            remoteHubSwitch = new RemoteHubSwitch();
            remoteHubSwitch.ConnectionErrorOccurred += RemoteHubSwitch_ConnectionErrorOccurred;
            remoteHubSwitch.AdapterRemoved += RemoteHubSwitch_AdapterRemoved;

            //Tcp server
            ClientEntity.InitializeServerCertificate(serverIP, "TestServer");
            tcpListener = new TcpListener(serverIP, serverPort);
            tcpListener.Start();
            var accepting = Task.Run(() => TcpServerAcceptLinks(shutdownSignal.Token));

            //Local client on RemoteHub
            var siteId = ServerId.SiteId;
            localClient = new RemoteHubSwitchDirect<string>(siteId, OnMessageReceivedFromRemoteHub);
            localClient.RemoteClientRemoved += RemoteHubClient_RemoteClientRemoved;
            remoteHubSwitch.AddAdapter(localClient);

            //Remote Agency
            remoteAgency = new RemoteAgencyManagerEncapsulated(false, true, siteId);
            remoteAgency.MessageForSendingPrepared += OnMessageForSendingPrepared;
            remoteAgency.AddServiceWrapper<IChatServer>(chatServer, ServerId.ServiceId);

            //localClient.Start(); //started already. when SwitchDirect is added to switch, it will be started automatically.
            remoteAgency.Connect();

            Console.WriteLine("Chat server is started. Press any key to quit.");
            Console.ReadKey(true);

            shutdownSignal.Cancel();
            tcpListener.Stop();
            remoteAgency.Disconnect(false);
            remoteAgency.Dispose();
            localClient.Stop();
            localClient.Dispose();
            remoteHubSwitch.RemoveAllAdapters();
            foreach (var client in clients)
            {
                client.Dispose();
            }
            clients.Clear();
        }

        private static void RemoteHubSwitch_AdapterRemoved(object sender, AdapterEventArgs e)
        {
            lock (clients)
            {
                var match = clients.Where(i => i.Adapter == e.Adapter).FirstOrDefault();
                if (match != null)
                {
                    clients.Remove(match);
                    Console.WriteLine("Hub: Remove Client: " + match.EndPoint);
                }
            }
        }

        private static void RemoteHubSwitch_ConnectionErrorOccurred(object sender, ConnectionExceptionWithAdapterEventArgs e)
        {
            Console.WriteLine("Connection Error: " + e.Exception.ToString());
            if (e.IsFatal)
            {
                var adapter = e.Adapter;
                remoteHubSwitch.RemoveAdapter(adapter);
            }
        }

        static void TcpServerAcceptLinks(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = tcpListener.AcceptTcpClient();
                    ClientEntity entity = new ClientEntity(tcpClient);
                    remoteHubSwitch.AddAdapter((StreamAdapter<byte[]>)entity.Adapter);
                    Console.WriteLine("Hub: Add Client: " + entity.EndPoint);
                    lock (clients)
                    {
                        clients.Add(entity);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        static void OnMessageForSendingPrepared(object sender, RemoteAgencyManagerMessageForSendingEventArgs<string> e)
        {
            localClient.SendMessage(e.TargetSiteId, e.Message);
        }

        static void OnMessageReceivedFromRemoteHub(Guid clientId, string text) //Note: clientId in the parameters is the target id of the message, not the source.
        {
            remoteAgency.ProcessPackagedMessage(text);
        }

        static void RemoteHubClient_RemoteClientRemoved(object sender, ClientIdEventArgs e)
        {
            //Unreg event when a remote client is offline. 
            remoteAgency.OnProxiesOfSiteDisposed(e.ClientId);
        }
    }
}
