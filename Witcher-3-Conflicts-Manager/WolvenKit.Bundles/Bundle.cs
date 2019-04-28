using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WolvenKit.Common;

namespace WolvenKit.Bundles
{
    public class Bundle : IWitcherArchiveType
    {
        private static readonly byte[] IDString =
        {
            (byte) 'P', (byte) 'O', (byte) 'T', (byte) 'A',
            (byte) 'T', (byte) 'O', (byte) '7', (byte) '0'
        };

        private static int HEADER_SIZE = 32;
        private static int ALIGNMENT_TARGET = 4096;
        private static string FOOTER_DATA = "AlignmentUnused"; //The bundle's final filesize should be an even multiple of 16; garbage data should be appended at the end if necessary to make this happen [appears to be unnecessary/optional, as far as the game cares]
        private static int TOCEntrySize = 0x100 + 16 + 4 + 4 + 4 + 4 + 8 + 16 + 4 + 4; //Size of a TOC Entry.

        private UInt16 bundlesize;
        private UInt16 unk1;
        private uint dataoffset;
        private uint dummysize;

        public uint DataBlockSize { get; set; }
        public uint DataBlockOffset { get; set; }

        public Bundle(string filename)
        {
            FileName = filename;
            Read();
        }

        public Bundle()
        {

        }

        public string TypeName { get { return "Bundle"; } }
        public string FileName { get; set; }
        public Dictionary<string, BundleItem> Items { get; set; }
        
        /// <summary>
        /// Reads the Table Of Contents of the bundle.
        /// </summary>
        private void Read()
        {
            Items = new Dictionary<string, BundleItem>();

            using (var reader = new BinaryReader(new FileStream(FileName, FileMode.Open, FileAccess.Read)))
            {
                var idstring = reader.ReadBytes(IDString.Length);

                if (!IDString.SequenceEqual(idstring))
                {
                    throw new InvalidBundleException("Bundle header mismatch.");
                }

                bundlesize = reader.ReadUInt16(); //this seems to be a Uint16
                unk1 = reader.ReadUInt16(); //2 bytes left over
                dummysize = reader.ReadUInt32();
                dataoffset = reader.ReadUInt32();

                DataBlockOffset = dataoffset + (uint)HEADER_SIZE;
                DataBlockSize = bundlesize - DataBlockOffset;

                reader.BaseStream.Seek(0x20, SeekOrigin.Begin);

                while (reader.BaseStream.Position < dataoffset + 0x20)
                {
                    var item = new BundleItem
                    {
                        Bundle = this
                    };

                    var strname = Encoding.Default.GetString(reader.ReadBytes(0x100));

                    item.Name = strname.Substring(0, strname.IndexOf('\0'));
                    item.Hash = reader.ReadBytes(16);
                    item.Empty = reader.ReadUInt32();
                    item.Size = reader.ReadUInt32();
                    item.ZSize = reader.ReadUInt32();
                    item.PageOFfset = reader.ReadUInt32();

                    var date = reader.ReadUInt32();
                    var y = date >> 20;
                    var m = date >> 15 & 0x1F;
                    var d = date >> 10 & 0x1F;

                    var time = reader.ReadUInt32();
                    var h = time >> 22;
                    var n = time >> 16 & 0x3F;
                    var s = time >> 10 & 0x3F;

                    item.DateString = string.Format(" {0}/{1}/{2} {3}:{4}:{5}", d, m, y, h, n, s);

                    item.Zero = reader.ReadBytes(16);    //00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 (always, in every archive)
                    item.CRC = reader.ReadUInt32();    //CRC32 for the uncompressed data
                    item.Compression = reader.ReadUInt32();

                    Items.Add(item.Name, item);
                }


                reader.Close();
            }
        }

        private static byte[] CompressFile(byte[] Data, int ComType)
        {
            switch (ComType)
            {
                case 4:
                case 5:
                    {
                        return LZ4.LZ4Codec.EncodeHC(Data, 0, Data.Length);
                    }
                default:
                    {
                        return Data;
                    }
            }
        }


        /// <summary>
        /// Packs a List of IWitcherFiles to a bundle.
        /// </summary>
        /// <param name="Outputpath">The path to save the bundle to with the packed files.</param>
        /// <param name="BundleItems">The List of Files to pack</param>
        public static void Write(string Outputpath, List<IWitcherFile> BundleItems)
        {
            // calculate ToC size
            UInt32 toCRealSize = (UInt32)(BundleItems.Count * TOCEntrySize);

            // MAIN BODY
            var uncompressedData = new List<byte[]>();
            var BODY = new List<KeyValuePair<uint, List<byte>>>();
            int offset = GetOffset((int)toCRealSize);
            for (int i = 0; i < BundleItems.Count; i++)
            {
                IWitcherFile item = (IWitcherFile)BundleItems[i];

                //get uncompressed data
                byte[] uc = item.UncompressedData();
                uncompressedData.Add(uc);

                //compress file
                List<byte> compressedFile = CompressFile(uc, (int)item.Compression).ToList();
                //padding
                var filesize = compressedFile.Count;
                var nextOffset = GetOffset(offset + filesize);
                int paddingLength = nextOffset - (offset + filesize);
                if (paddingLength > 0 && i < (BundleItems.Count - 1)) //don't pad the last item
                    compressedFile.AddRange(new byte[paddingLength]);

                BODY.Add(new KeyValuePair<uint, List<byte>>((uint)offset, compressedFile));

                offset = nextOffset;
            }

            
            using (var fs = new FileStream(Outputpath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                var dummysize = 0; //May not need to be recomputed. TODO: Investigate
                UInt32 bundleSize = (UInt32)(offset);    //last offset is the offset of the end of the last compressed file entry

                // Write Header
                bw.Write(IDString);
                bw.Write(bundleSize); //TODO for buffers this is just 4095??
                bw.Write(dummysize);
                bw.Write(toCRealSize);
                bw.Write(new byte[] { 0x03, 0x00, 0x01, 0x00, 0x00, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13 }); //TODO: Figure out what the hell is this.

                // Write ToC
                var minDataOffset = ALIGNMENT_TARGET;
                for (int i = 0; i < BundleItems.Count; i++)
                {
                    IWitcherFile f = BundleItems[i];
                    var dataOffset = BODY[i].Key;

                    var name = Encoding.Default.GetBytes(f.Name).ToArray();
                    if (name.Length > 0x100)
                        name = name.Take(0x100).ToArray();
                    if (name.Length < 0x100)
                        Array.Resize(ref name, 0x100);
                    bw.Write(name); //Filename trimmed to 100 characters.

                    bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); //HASH
                    bw.Write((UInt32)0x00000000); //EMPTY
                    bw.Write((UInt32)f.Size); //SIZE
                    bw.Write((UInt32)f.ZSize); //ZSIZE
                    bw.Write((UInt32)dataOffset); //DATA OFFSET
                    bw.Write((UInt32)0x00000000); //DATE
                    bw.Write((UInt32)0x00000000); //TIME
                    bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); //PADDING
                    UInt32 crc = Crc32C.Crc32CAlgorithm.Compute(uncompressedData[i]);
                    bw.Write((UInt32)crc); //CRC32 FIXME: Check if the game actually cares. crc is incorrect
                    bw.Write((UInt32)f.Compression); // Compression.
                }

                //pad the ToC
                int writePosition = (int)bw.BaseStream.Position;
                int paddingLength = GetOffset(writePosition) - writePosition;
                if (paddingLength > 0)
                    bw.Write(new byte[paddingLength]);

                // Write Body
                foreach (var item in BODY)
                {
                    bw.Write(item.Value.ToArray());
                }
            }
        }

        /// <summary>
        /// Packs files to a bundle.
        /// </summary>
        /// <param name="Outputpath">The path to save the bundle to with the packed files.</param>
        /// <param name="Files">The Files to pack</param>
        public static void Write(string Outputpath, string rootfolder)
        {
            throw new NotImplementedException();
        }

        public static int WriteCompressedData(BinaryWriter bw, byte[] Data,int ComType)
        {
            int writePosition = (int)bw.BaseStream.Position;
            int numWritten = 0;
            int paddingLength = GetOffset(writePosition) - writePosition;
            if (paddingLength > 0)
            {
                /*
                int preliminaryPaddingLength = 16;      //use of 'prelimanary padding' data appears to be optional as far as the game cares, so don't bother with it [this line disables it]
                //int preliminaryPaddingLength = 16 - (writePosition % 16);
                if (preliminaryPaddingLength < 16)
                {
                    bw.Write(FOOTER_DATA.Substring(0, preliminaryPaddingLength));
                    paddingLength -= preliminaryPaddingLength;
                    numWritten += preliminaryPaddingLength;
                }
                */
                if (paddingLength > 0)
                {
                    bw.Write(new byte[paddingLength]);
                    numWritten += paddingLength;
                }
            }
            switch (ComType)
            {
                case 4:
                case 5:
                {
                    bw.Write(LZ4.LZ4Codec.EncodeHC(Data,0,Data.Length));
                    break;
                }
                default:
                {
                    bw.Write(Data);
                    numWritten += Data.Length;
                    break;
                }
            }
            return numWritten;
        }

        public static int GetOffset(int minPos)
        {
            int firstValidPos = (minPos / ALIGNMENT_TARGET) * ALIGNMENT_TARGET + ALIGNMENT_TARGET;
            while (firstValidPos < minPos)
            {
                firstValidPos += ALIGNMENT_TARGET;
            }
            return firstValidPos;
        }

        public static int GetCompressedSize(byte[] content)
        {
            return LZ4.LZ4Codec.EncodeHC(content, 0, content.Length).Length;
        }

        /// <summary>
        /// Gets relative path from absolute path.
        /// </summary>
        /// <param name="filespec">A files path.</param>
        /// <param name="folder">The folder's path.</param>
        /// <returns></returns>
        public static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}