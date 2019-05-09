using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    abstract class ValueConverter<T>
    {
        internal abstract T ConvertFromMessage(byte[] value);
        internal abstract byte[] ConvertToMessage(T value);

        public static ValueConverter<T> Create(Encoding encoding = null)
        {
            var type = typeof(T);
            if (type == typeof(string))
            {
                if (encoding == null)
                    encoding = Encoding.Default;
                ValueConverter<string> client = new ValueConverterOfString(encoding);
                return __refvalue(__makeref(client), ValueConverter<T>);
            }
            else if (type == typeof(byte[]))
            {
                ValueConverter<byte[]> client = new ValueConverterOfByteArray();
                return __refvalue(__makeref(client), ValueConverter<T>);
            }
            else
            {
                throw new NotSupportedException("Only string and byte array are supported.");
            }
        }
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
