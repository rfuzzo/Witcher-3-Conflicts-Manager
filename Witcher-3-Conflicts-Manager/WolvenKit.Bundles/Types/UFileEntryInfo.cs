using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UFileEntryInfo : ISerializable
    {
        public UInt32 FileID = 0;
        public UInt32 BundleID = 0;
        public UInt32 OffsetInBundle = 0;
        public UInt32 SizeInBundle = 0;
        public UInt32 NextEntry = 0;

        public void Deserialize(BinaryReader reader)
        {
            FileID = reader.ReadUInt32();
            BundleID = reader.ReadUInt32();
            OffsetInBundle = reader.ReadUInt32();
            SizeInBundle = reader.ReadUInt32();
            NextEntry = reader.ReadUInt32();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((UInt32)FileID);
            writer.Write((UInt32)BundleID);
            writer.Write((UInt32)OffsetInBundle);
            writer.Write((UInt32)SizeInBundle);
            writer.Write((UInt32)NextEntry);
        }
    }
}
