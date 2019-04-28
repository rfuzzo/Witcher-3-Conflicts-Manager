using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UHash : ISerializable
    {
        public UInt64 Hash = 0;
        public UInt64 FileID = 0;

        public void Deserialize(BinaryReader reader)
        {
            Hash = reader.ReadUInt64();
            FileID = reader.ReadUInt64();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((UInt64)Hash);
            writer.Write((UInt64)FileID);
        }
    }
}
