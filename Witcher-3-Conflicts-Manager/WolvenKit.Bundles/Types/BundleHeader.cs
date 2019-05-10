using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{

    public class BundleHeader
    {
        public byte[] IDString;
        public uint Bundlesize;
        private uint Dummysize;
        public uint TocRealSize;

        public BundleHeader()
        {

        }

        public BundleHeader(byte[] idstring, uint bundlesize, uint dummysize, uint tocrealsize)
        {
            IDString = idstring;
            Bundlesize = bundlesize;
            Dummysize = dummysize;
            TocRealSize = tocrealsize;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(IDString);
            bw.Write(Bundlesize);
            bw.Write(Dummysize);
            bw.Write(TocRealSize);
            bw.Write(new byte[] { 0x03, 0x00, 0x01, 0x00, 0x00, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13 }); //TODO: Figure out what the hell is this.
        }

        public void Read(BinaryReader br)
        {
            IDString = br.ReadBytes(8);
            Bundlesize = br.ReadUInt32();
            Dummysize = br.ReadUInt32();
            TocRealSize = br.ReadUInt32();
        }
    }
}
