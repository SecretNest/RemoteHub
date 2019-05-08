using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SharedInterface
{
    [DataContract]
    public class MessageReceivedEventArgs : EventArgs
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Text { get; set; }

        public MessageReceivedEventArgs(string name, string text)
        {
            Name = name;
            Text = text;
        }
    }
}
