using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    abstract class StreamClient<T>
    {
        Stream stream;
        readonly Guid clientId;

        public Guid ClientId => clientId;

        byte[] currentVirtualHostSettingIdData;
        byte[] currentVirtualHostSettingData;
        bool needRefreshFull = false;
        TimeSpan hostTimeToLive = new TimeSpan(0, 0, 30);
        TimeSpan hostRefreshingTime = new TimeSpan(0, 0, 15);
        readonly byte[] clientIdData;
        readonly byte[] messageDatagramShutdown, messageDatagramRefresh;
        byte[] messageDatagramRefreshFull;

        protected StreamClient(Guid clientId, Stream stream)
        {
            this.clientId = clientId;
            this.stream = stream;

            clientIdData = clientId.ToByteArray();

            messageDatagramShutdown = new byte[17];
            messageDatagramShutdown[0] = 255;
            Array.Copy(clientIdData, 0, messageDatagramShutdown, 1, 16);

            messageDatagramRefresh = new byte[35];
            messageDatagramRefresh[0] = 129;
            Array.Copy(clientIdData, 0, messageDatagramRefresh, 1, 16);

            currentVirtualHostSettingIdData = new byte[16];
            currentVirtualHostSettingData = new byte[16];

        }


        public TimeSpan HostTimeToLive
        {
            get
            {
                return hostTimeToLive;
            }
            set
            {
                if (value.TotalSeconds < 5)
                    hostTimeToLive = new TimeSpan(0, 0, 5);
                else
                    hostTimeToLive = value;
                BuildMessageDatagram();
                hostRefreshingTime = new TimeSpan(hostTimeToLive.Ticks / 2);
            }
        }
               
        void BuildMessageDatagram()
        {
            var ttl = (ushort)hostTimeToLive.TotalSeconds;
            Array.Copy(BitConverter.GetBytes(ttl), 0, messageDatagramRefresh, 17, 2);
            Array.Copy(currentVirtualHostSettingIdData, 0, messageDatagramRefresh, 19, 16);

        }


    }
}
