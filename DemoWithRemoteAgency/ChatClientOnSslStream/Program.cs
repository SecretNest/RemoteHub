using SecretNest.RemoteAgency;
using SecretNest.RemoteHub;
using SharedInterface;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ChatClientOnSslStream
{
    class Program
    {
        static readonly IPAddress serverIP = IPAddress.Loopback;
        static readonly int serverPort = 34567;

        static RemoteHubOverStream<string> remoteHub;
        static RemoteAgencyManagerEncapsulated remoteAgency;

        static void Main(string[] args)
        {
            //Init
            Console.Write("Enter your name and press enter <Empty = Exit>: ");
            var name = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(name))
                return;

            //Stream
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(serverIP, serverPort);

            var clientCertificate = BuildSelfSignedClientCertificate("localClient");
            var clientCertificateCollection = new X509CertificateCollection(new X509Certificate[] { clientCertificate });

            SslStream stream1 = new SslStream(tcpClient.GetStream(), false,
                (sender, certificate, chain, sslPolicyErrors) => true /*always return true in certificate test in this demo*/);
            stream1.AuthenticateAsClient(serverIP.ToString(), clientCertificateCollection, false);

            //Remote Hub
            var siteId = Guid.NewGuid();
            remoteHub = new RemoteHubOverStream<string>(siteId, stream1, stream1, OnMessageReceivedFromHub);

            //Remote Agency
            remoteAgency = new RemoteAgencyManagerEncapsulated(true, false, siteId);
            remoteAgency.MessageForSendingPrepared += OnMessageForSendingPrepared;
            remoteAgency.DefaultTargetSiteId = ServerId.SiteId;
            var chatServer = remoteAgency.AddProxy<IChatServer>(ServerId.ServiceId, out var instanceId);

            remoteHub.Start();
            remoteAgency.Connect();

            chatServer.MessageReceived += ChatServer_MessageReceived;

            Console.WriteLine("Started. Please chat and press enter. Empty = Exit.");

            while (true)
            {
                var text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    break;
                chatServer.SendMessage(name, text);
            }

            ((IDisposable)chatServer).Dispose();

            remoteAgency.Disconnect(false);
            remoteHub.Stop();
            remoteAgency.Dispose();
            remoteHub.Dispose();
            stream1.Close();
            stream1.Dispose();
            tcpClient.Close();
            tcpClient.Dispose();
        }

        private static void ChatServer_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Console.WriteLine("{0} says: {1}", e.Name, e.Text);
        }

        static void OnMessageForSendingPrepared(object sender, RemoteAgencyManagerMessageForSendingEventArgs<string> e)
        {
            remoteHub.SendMessage(e.TargetSiteId, e.Message);
        }

        static void OnMessageReceivedFromHub(Guid clientId, string text) //Note: clientId in the parameters is the target id of the message, not the source.
        {
            remoteAgency.ProcessPackagedMessage(text);
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
    }
}
