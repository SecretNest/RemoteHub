using SecretNest.RemoteAgency;
using SecretNest.RemoteHub;
using SharedInterface;
using System;

namespace ChatServerOnRedis
{
    class Program
    {
        const string connectionString = "localhost"; //redis connection
        static RemoteHubOverRedis<string> remoteHub;
        static RemoteAgencyManagerEncapsulated remoteAgency;

        static void Main(string[] args)
        {
            //Create chat server instance
            var chatServer = new ChatServer();

            //Remote Hub
            var siteId = ServerId.SiteId;
            remoteHub = new RemoteHubOverRedis<string>(siteId, connectionString, OnMessageReceivedFromRemoteHub);
            remoteHub.RemoteClientRemoved += RemoteHub_RemoteClientRemoved;

            //Remote Agency
            remoteAgency = new RemoteAgencyManagerEncapsulated(false, true, siteId);
            remoteAgency.MessageForSendingPrepared += OnMessageForSendingPrepared;
            remoteAgency.AddServiceWrapper<IChatServer>(chatServer, ServerId.ServiceId);

            remoteHub.Start();
            remoteAgency.Connect();

            Console.WriteLine("Chat server is started. Press any key to quit.");
            Console.ReadKey(true);

            remoteAgency.Disconnect(false);
            remoteHub.Stop();
            remoteAgency.Dispose();
            remoteHub.Dispose();
        }

        static void OnMessageForSendingPrepared(object sender, RemoteAgencyManagerMessageForSendingEventArgs<string> e)
        {
            remoteHub.SendMessage(e.TargetSiteId, e.Message);
            //Console.WriteLine("Sent");
        }

        static void OnMessageReceivedFromRemoteHub(Guid clientId, string text) //Note: clientId in the parameters is the target id of the message, not the source.
        {
            //Console.WriteLine("Receive");
            remoteAgency.ProcessPackagedMessage(text);
        }

        static void RemoteHub_RemoteClientRemoved(object sender, ClientIdEventArgs e)
        {
            //Unreg event when a remote client is offline. 
            remoteAgency.OnProxiesOfSiteDisposed(e.ClientId);
        }
    }
}
