using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UDirInitInfo : ISerializable
    {
        public Int32 Name = 0;
        public Int32 ParentID = 0;

        public void Deserialize(BinaryReader reader)
        {
            Name = reader.ReadInt32();
            ParentID = reader.ReadInt32();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((UInt32)Name);
            writer.Write((UInt32)ParentID);
        }
    }
}
