using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SecretNest.RemoteHub
{
    static class StreamWritingExtensions
    {
        //static async Task<byte[]> ReadBinary(this Stream stream, int length)
        //{
        //    byte[] buffer = new byte[length];
        //    int result = await stream.ReadAsync(buffer, 0, length);
        //    if (result < length)
        //        throw new EndOfStreamException();
        //    return buffer;
        //}

        //public static async Task Skip24Bytes(this Stream stream, int count)
        //{
        //    byte[] buffer = new byte[24];
        //    for (int i = 0; i < count; i++)
        //        await stream.ReadAsync(buffer, 0, 24);
        //}

        //public static async Task<Guid> ReadGuid(this Stream stream)
        //{
        //    return new Guid(await stream.ReadBinary(16));
        //}

        public static async Task WriteGuid(this Stream stream, Guid value)
        {
            await stream.WriteAsync(value.ToByteArray(), 0, 16);
        }

        //public static async Task<ushort> ReadUInt16(this Stream stream)
        //{
        //    byte[] buffer = await stream.ReadBinary(2);
        //    return BitConverter.ToUInt16(buffer, 0);
        //}

        public static async Task WriteUInt16(this Stream stream, ushort value)
        {
            await stream.WriteAsync(BitConverter.GetBytes(value), 0, 2);
        }

        //public static async Task<int> ReadInt32(this Stream stream)
        //{
        //    byte[] buffer = await stream.ReadBinary(4);
        //    return BitConverter.ToInt32(buffer, 0);
        //}

        public static async Task WriteInt32(this Stream stream, int value)
        {
            await stream.WriteAsync(BitConverter.GetBytes(value), 0, 4);
        }
    }
}
