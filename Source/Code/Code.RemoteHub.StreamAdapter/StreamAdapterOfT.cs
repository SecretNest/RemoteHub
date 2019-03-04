using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Converts between RemoteHub commands and stream.
    /// </summary>
    /// <typeparam name="T">Type of the message data. Only string and byte array (byte[]) are supported.</typeparam>
    public class StreamAdapter<T> : StreamAdapter, IRemoteHubAdapter<T>
    {
        OnMessageReceivedCallback<T> onMessageReceivedCallback;
        ValueConverter<T> valueConverter;

        /// <summary>
        /// Initializes an instance of StreamAdapter.
        /// </summary>
        /// <param name="inputStream">Stream for reading.</param>
        /// <param name="outputStream">Stream for writing.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message.</param>
        /// <param name="refreshingIntervalInSeconds">The interval in seconds before sending a data package for keeping it alive when streams are idle.</param>
        /// <param name="encoding">The encoder for converting between string and byte array. Default value is Encoding.Default. Will be ignored if type is not string.</param>
        public StreamAdapter(Stream inputStream, Stream outputStream, OnMessageReceivedCallback<T> onMessageReceivedCallback, int refreshingIntervalInSeconds, Encoding encoding = null)
            : base(inputStream, outputStream, refreshingIntervalInSeconds)
        {
            this.onMessageReceivedCallback = onMessageReceivedCallback;

            var type = typeof(T);
            if (type == typeof(string))
            {
                if (encoding == null)
                    encoding = Encoding.Default;
                ValueConverter<string> client = new ValueConverterOfString(encoding);
                valueConverter = __refvalue(__makeref(client), ValueConverter<T>);
            }
            else if (type == typeof(byte[]))
            {
                ValueConverter<byte[]> client = new ValueConverterOfByteArray();
                valueConverter = __refvalue(__makeref(client), ValueConverter<T>);
            }
            else
            {
                throw new NotSupportedException("Only string and byte array are supported.");
            }
        }

        /// <inheritdoc/>
        public void SendPrivateMessage(Guid remoteClientId, T message)
        {
            if (IsSelf(remoteClientId))
            {
                OnPrivateMessageReceived(remoteClientId, message);
            }
            else
            {
                SendingPrivateMessage(remoteClientId, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        public Task SendPrivateMessageAsync(Guid remoteClientId, T message)
        {
            return Task.Run(() => SendPrivateMessage(remoteClientId, message));
        }

        protected override void OnPrivateMessageReceived(Guid targetClientId, byte[] dataPackage)
        {
            OnPrivateMessageReceived(targetClientId, valueConverter.ConvertFromMessage(dataPackage));
        }

        void OnPrivateMessageReceived(Guid targetClientId, T message)
        {
            Task.Run(() => onMessageReceivedCallback(targetClientId, message));
        }
    }
}
