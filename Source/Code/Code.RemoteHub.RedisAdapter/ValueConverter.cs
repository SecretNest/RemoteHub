using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    abstract class ValueConverter<T>
    {
        internal abstract T ConvertFromMessage(RedisValue value);
        internal abstract RedisValue ConvertToMessage(T value);

        internal static ValueConverter<T> Create()
        {
            var type = typeof(T);
            if (type == typeof(string))
            {
                ValueConverter<string> client = new ValueConverterOfString();
                return __refvalue(__makeref(client), ValueConverter<T>);
            }
            else if (type == typeof(byte[]))
            {
                ValueConverter<byte[]> client = new ValueConverterOfByteArray();
                return __refvalue(__makeref(client), ValueConverter<T>);

            }
            else
            {
                throw new NotSupportedException("Only string and byte array is supported.");
            }
        }
    }

    class ValueConverterOfString : ValueConverter<string>
    {
        internal override string ConvertFromMessage(RedisValue value)
        {
            return value;
        }

        internal override RedisValue ConvertToMessage(string value)
        {
            return value;
        }
    }

    class ValueConverterOfByteArray : ValueConverter<byte[]>
    {
        internal override byte[] ConvertFromMessage(RedisValue value)
        {
            return value;
        }

        internal override RedisValue ConvertToMessage(byte[] value)
        {
            return value;
        }
    }
}
