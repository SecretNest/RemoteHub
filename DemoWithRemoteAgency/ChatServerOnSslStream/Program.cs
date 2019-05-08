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
        static TcpClient localClientTcpClient;
        static SslStream localClientSslStream;
        static RemoteHubOverStream<string> localClient;
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
            localClient = BuildLocalClient();
            localClient.RemoteClientRemoved += RemoteHubClient_RemoteClientRemoved;

            //Remote Agency
            var siteId = ServerId.SiteId;
            remoteAgency = new RemoteAgencyManagerEncapsulated(false, true, siteId);
            remoteAgency.MessageForSendingPrepared += OnMessageForSendingPrepared;
            remoteAgency.AddServiceWrapper<IChatServer>(chatServer, ServerId.ServiceId);

            localClient.Start();
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
                    remoteHubSwitch.AddAdapter(entity.Adapter);
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

        static RemoteHubOverStream<string> BuildLocalClient()
        {
            localClientTcpClient = new TcpClient();
            localClientTcpClient.Connect(serverIP, serverPort);
            localClientSslStream = new SslStream(localClientTcpClient.GetStream(), false,
                (sender, certificate, chain, sslPolicyErrors) => true /*always return true in certificate test in this demo*/);
            var clientCertificate = BuildSelfSignedClientCertificate(Guid.Empty.ToString());
            var clientCertificateCollection = new X509CertificateCollection(new X509Certificate[] { clientCertificate });
            localClientSslStream.AuthenticateAsClient(serverIP.ToString(), clientCertificateCollection, false);
            return new RemoteHubOverStream<string>(ServerId.SiteId, localClientSslStream, localClientSslStream, OnMessageReceivedFromRemoteHub);
        }

        static X509Certificate2 BuildSelfSignedClientCertificate(string certificateName)
        {
            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={certificateName}");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(
                   new X509EnhancedKeyUsageExtension(
                       new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false));

                var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
                certificate.FriendlyName = certificateName;

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "SomePassword"), "SomePassword", X509KeyStorageFlags.MachineKeySet);
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
