using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Cache.Types
{
    public class TextureCacheFooter
    {
        public UInt64 Crc;
        public UInt32 UsedPages;
        public UInt32 EntryCount;
        public UInt32 StringTableSize;
        public UInt32 MipEntryCount;
        public byte[] IDString;
        public UInt32 Version = 6;

        public TextureCacheFooter()
        {

        }


        public void Write(BinaryWriter bw)
        {
            bw.Write(Crc);
            bw.Write(UsedPages);
            bw.Write(EntryCount);
            bw.Write(StringTableSize);
            bw.Write(MipEntryCount);
            bw.Write(IDString);
            bw.Write(Version);
        }

        public void Read(BinaryReader br)
        {
            Crc = br.ReadUInt64();
            UsedPages = br.ReadUInt32();
            EntryCount = br.ReadUInt32();
            StringTableSize = br.ReadUInt32();
            MipEntryCount = br.ReadUInt32();
            IDString = br.ReadBytes(4);
            Version = br.ReadUInt32();
        }
    }
}
