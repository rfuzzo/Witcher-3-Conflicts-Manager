using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    public class UFileInitInfo : ISerializable
    {
        public Int32 FileIF = 0;
        public Int32 DirID = 0;
        public Int32 Name = 0;

        public void Deserialize(BinaryReader reader)
        {
            FileIF = reader.ReadInt32();
            DirID = reader.ReadInt32();
            Name = reader.ReadInt32();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((Int32)FileIF);
            writer.Write((Int32)DirID);
            writer.Write((Int32)Name);
        }
    }
}
