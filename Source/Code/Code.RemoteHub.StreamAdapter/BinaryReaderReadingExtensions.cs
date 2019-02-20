using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    static class BinaryReaderReadingExtensions
    {
        public static void Skip24Bytes(this BinaryReader reader, int count)
        {
            reader.ReadBytes(24 * count);
        }

        public static Guid ReadGuid(this BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(16);
            return new Guid(buffer);
        }

        public static ushort ReadUInt16(this BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(2);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static int ReadInt32(this BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(4);
            return BitConverter.ToInt32(buffer, 0);
        }
    }
}
