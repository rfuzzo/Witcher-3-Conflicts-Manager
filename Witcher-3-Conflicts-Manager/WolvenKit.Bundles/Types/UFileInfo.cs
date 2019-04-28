using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UFileInfo : ISerializable
    {
        public string path;

        public UInt32 StringTableNameOffset = 0;
        public UInt32 PathHash = 0;
        public UInt32 SizeInBundle = 0;
        public UInt32 SizeInMemory = 0;
        public UInt32 FirstEntry = 0;
        public UInt32 CompressionType = 0;
        public UInt32 bufferid = 0;
        public UInt32 hasbuffer = 0;

        public void Deserialize(BinaryReader reader)
        {
            StringTableNameOffset = reader.ReadUInt32();
            PathHash = reader.ReadUInt32();
            SizeInBundle = reader.ReadUInt32();
            SizeInMemory = reader.ReadUInt32();
            FirstEntry = reader.ReadUInt32();
            CompressionType = reader.ReadUInt32();
            bufferid = reader.ReadUInt32();
            hasbuffer = reader.ReadUInt32();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((UInt32)StringTableNameOffset);
            writer.Write((UInt32)PathHash);
            writer.Write((UInt32)SizeInBundle);
            writer.Write((UInt32)SizeInMemory);
            writer.Write((UInt32)FirstEntry);
            writer.Write((UInt32)CompressionType);
            writer.Write((UInt32)bufferid);
            writer.Write((UInt32)hasbuffer);
        }
    }
}
