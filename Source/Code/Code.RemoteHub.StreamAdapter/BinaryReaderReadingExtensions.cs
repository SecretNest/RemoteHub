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

        public static ushort ReadUInt16LittleEndian(this BinaryReader reader)
        {
            return (ushort)((reader.ReadByte() << 8) + reader.ReadByte());
        }

        public static int ReadInt32LittleEndian(this BinaryReader reader)
        {
            return (reader.ReadByte() << 24) + (reader.ReadByte() << 16) + (reader.ReadByte() << 8) + reader.ReadByte();
        }


    }
}
