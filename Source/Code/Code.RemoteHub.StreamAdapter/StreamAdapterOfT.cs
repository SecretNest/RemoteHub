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
        PrivateMessageCallbackHelper<T> privateMessageCallbackHelper;
        ValueConverter<T> valueConverter;

        /// <summary>
        /// Initializes an instance of StreamAdapter.
        /// </summary>
        /// <param name="inputStream">Stream for reading.</param>
        /// <param name="outputStream">Stream for writing.</param>
        /// <param name="onMessageReceivedCallback">The callback to be used for dealing received private message. Default value is null.</param>
        /// <param name="refreshingIntervalInSeconds">The interval in seconds before sending a data package for keeping it alive when streams are idle. Default value is 60 seconds.</param>
        /// <param name="encoding">The encoder for converting between string and byte array. Default value is Encoding.Default. Will be ignored if type is not string.</param>
        public StreamAdapter(Stream inputStream, Stream outputStream, OnMessageReceivedCallback<T> onMessageReceivedCallback = null, int refreshingIntervalInSeconds = 60, Encoding encoding = null)
            : base(inputStream, outputStream, refreshingIntervalInSeconds)
        {
            privateMessageCallbackHelper = new PrivateMessageCallbackHelper<T>(onMessageReceivedCallback);
            valueConverter = ValueConverter<T>.Create(encoding);
        }

        /// <inheritdoc/>
        public void AddOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.AddCallback(callback);
            }
        }

        /// <inheritdoc/>
        public void RemoveOnMessageReceivedCallback(OnMessageReceivedCallback<T> callback)
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.RemoveCallback(callback);
            }
        }

        /// <inheritdoc/>
        public void RemoveAllOnMessageReceivedCallbacks()
        {
            lock (privateMessageCallbackHelper)
            {
                privateMessageCallbackHelper.RemoveAllCallbacks();
            }
        }

        /// <inheritdoc/>
        public void SendPrivateMessage(Guid clientId, T message)
        {
            if (IsSelf(clientId))
            {
                OnPrivateMessageReceived(clientId, message);
            }
            else
            {
                SendingPrivateMessage(clientId, valueConverter.ConvertToMessage(message));
            }
        }

        /// <inheritdoc/>
        public async Task SendPrivateMessageAsync(Guid clientId, T message)
        {
            if (IsSelf(clientId))
            {
                await OnPrivateMessageReceivedAsync(clientId, message);
            }
            else
            {
                await Task.Run(() => SendingPrivateMessage(clientId, valueConverter.ConvertToMessage(message)));
            }
        }

        /// <inheritdoc/>
        protected override void OnPrivateMessageReceived(Guid targetClientId, byte[] dataPackage)
        {
            OnPrivateMessageReceived(targetClientId, valueConverter.ConvertFromMessage(dataPackage));
        }

        void OnPrivateMessageReceived(Guid targetClientId, T message)
        {
            privateMessageCallbackHelper.CallAndForget(targetClientId, message);
        }

        async Task OnPrivateMessageReceivedAsync(Guid targetClientId, T message)
        {
            await privateMessageCallbackHelper.CallAsync(targetClientId, message);
        }
    }
}
