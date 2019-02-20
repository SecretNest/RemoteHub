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
