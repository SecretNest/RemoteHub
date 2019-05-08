using SecretNest.RemoteHub;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ChatServerOnSslStream
{
    class ClientEntity : IDisposable
    {
        public TcpClient TcpClient { get; }
        public SslStream Stream { get; }
        public StreamAdapter<byte[]> Adapter { get; }

        public string EndPoint { get; }

        public ClientEntity(TcpClient tcpClient)
        {
            try
            {
                var stream = new SslStream(tcpClient.GetStream(), false,
                    (sender, certificate, chain, sslPolicyErrors) => true /*always return true in certificate test in this demo*/);
                try
                {
                    EndPoint = tcpClient.Client.RemoteEndPoint.ToString();
                    stream.AuthenticateAsServer(serverCertificate);
                    
                    TcpClient = tcpClient;
                    Stream = stream;
                    Adapter = new StreamAdapter<byte[]>(stream, stream);
                }
                catch
                {
                    stream.Close();
                    stream.Dispose();
                    throw;
                }
            }
            catch
            {
                tcpClient.Close();
                tcpClient.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Adapter.Stop();
            Adapter.Dispose();
            Stream.Close();
            Stream.Dispose();
            TcpClient.Close();
            TcpClient.Dispose();
        }

        public static void InitializeServerCertificate(IPAddress ipAddress, string certificateName)
        {
            serverCertificate = BuildSelfSignedServerCertificate(ipAddress, certificateName);
        }

        static X509Certificate2 serverCertificate;

        static X509Certificate2 BuildSelfSignedServerCertificate(IPAddress ipAddress, string certificateName)
        {
            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(ipAddress);
            //sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName(certificateName);
            //sanBuilder.AddDnsName("localhost");
            //sanBuilder.AddDnsName(Environment.MachineName);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={certificateName}");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(
                   new X509EnhancedKeyUsageExtension(
                       new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                request.CertificateExtensions.Add(sanBuilder.Build());

                var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
                certificate.FriendlyName = certificateName;

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "SomePassword"), "SomePassword", X509KeyStorageFlags.MachineKeySet);
            }
        }
    }
}
