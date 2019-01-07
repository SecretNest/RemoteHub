using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecretNest.RemoteHub
{
    static class BinaryReaderWriterExtension
    {
        public static Guid ReadGuid(this BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(16);
            if (buffer.Length < 16)
                throw new EndOfStreamException();
            return new Guid(buffer);
        }

        public static void WriteGuid(this BinaryWriter writer, Guid value)
        {
            writer.Write(value.ToByteArray());
        }
    }
}
