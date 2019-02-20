using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    abstract class ValueConverter<T>
    {
        internal abstract T ConvertFromMessage(byte[] value);
        internal abstract byte[] ConvertToMessage(T value);
    }

    class ValueConverterOfString : ValueConverter<string>
    {
        readonly Encoding encoding;
        public ValueConverterOfString(Encoding encoding)
        {
            this.encoding = encoding;
        }

        internal override string ConvertFromMessage(byte[] value)
        {
            return encoding.GetString(value);
        }

        internal override byte[] ConvertToMessage(string value)
        {
            return encoding.GetBytes(value);
        }
    }

    class ValueConverterOfByteArray : ValueConverter<byte[]>
    {
        internal override byte[] ConvertFromMessage(byte[] value)
        {
            return value;
        }

        internal override byte[] ConvertToMessage(byte[] value)
        {
            return value;
        }
    }
}
