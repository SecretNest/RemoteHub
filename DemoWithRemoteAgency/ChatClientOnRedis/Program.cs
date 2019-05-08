using SecretNest.RemoteAgency;
using SecretNest.RemoteHub;
using SharedInterface;
using System;

namespace ChatClientOnRedis
{
    class Program
    {
        const string connectionString = "localhost"; //redis connection
        static RemoteHubOverRedis<string> remoteHub;
        static RemoteAgencyManagerEncapsulated remoteAgency;

        static void Main(string[] args)
        {
            //Init
            Console.Write("Enter your name and press enter <Empty = Exit>: ");
            var name = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(name))
                return;

            //Remote Hub
            var siteId = Guid.NewGuid();
            remoteHub = new RemoteHubOverRedis<string>(siteId, connectionString, OnMessageReceivedFromHub);

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
    }
}
