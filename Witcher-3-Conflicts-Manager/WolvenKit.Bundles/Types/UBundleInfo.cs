using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UBundleInfo : ISerializable
    {
        public UInt32 Name = 0;
        public UInt32 FirstFileEntry = 0;
        public UInt32 NumBundleEntries = 0;
        public UInt32 DataBlockSize = 0;
        public UInt32 DataBlockOffset = 0;
        public UInt32 BurstDataBlockSize = 0;

        public void Deserialize(BinaryReader reader)
        {
            Name = reader.ReadUInt32();
            FirstFileEntry = reader.ReadUInt32();
            NumBundleEntries = reader.ReadUInt32();
            DataBlockSize = reader.ReadUInt32();
            DataBlockOffset = reader.ReadUInt32();
            BurstDataBlockSize = reader.ReadUInt32();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((UInt32)Name);
            writer.Write((UInt32)FirstFileEntry);
            writer.Write((UInt32)NumBundleEntries);
            writer.Write((UInt32)DataBlockSize);
            writer.Write((UInt32)DataBlockOffset);
            writer.Write((UInt32)BurstDataBlockSize);
        }
    }
}
