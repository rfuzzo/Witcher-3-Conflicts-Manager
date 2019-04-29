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
        #region Info
        private static readonly byte[] IDString =
        {
            (byte) 'P', (byte) 'O', (byte) 'T', (byte) 'A',
            (byte) 'T', (byte) 'O', (byte) '7', (byte) '0'
        };

        private static int HEADER_SIZE = 32;
        private static int ALIGNMENT_TARGET = 4096;
        private static string FOOTER_DATA = "AlignmentUnused"; //The bundle's final filesize should be an even multiple of 16; garbage data should be appended at the end if necessary to make this happen [appears to be unnecessary/optional, as far as the game cares]
        private static int TOCEntrySize = 0x100 + 16 + 4 + 4 + 4 + 4 + 8 + 16 + 4 + 4; //Size of a TOC Entry.

        private uint bundlesize;
        private uint tocRealSize;
        private uint dummysize;
        #endregion

        #region Properties
        public uint DataBlockSize { get; set; }
        public uint DataBlockOffset { get; set; }
        public string TypeName { get { return "Bundle"; } }
        public string FileName { get; set; }
        public string Name { get; set; }
        public Dictionary<string, BundleItem> Items { get; set; }
        public List<IWitcherFile> ItemsList { get; set; } = new List<IWitcherFile>();
        #endregion

        #region Fields

        List<byte[]> uncompressedData { get; set; } = new List<byte[]>();
        List<KeyValuePair<uint, List<byte>>> BODY { get; set; } = new List<KeyValuePair<uint, List<byte>>>();
        #endregion

        public Bundle()
        {

        }
        public Bundle(string FilePath)
        {
            Read(FilePath);
        }
        public Bundle(IWitcherFile[] Files)
        {
            Read(Files);

            //check for buffers
            var bufferCount = ItemsList.Where(_ => _.Name.Split('\\').Last().Split('.').Last() == "buffer").ToList().Count;
            if (bufferCount > 0)
            {
                if (bufferCount == ItemsList.Count)
                    Name = "buffers0.bundle";
                else
                    throw new InvalidBundleException("Buffers and files mixed in one bundle.");
            }
            else
                Name = "blob0.bundle";
        }

        
        
        
        /// <summary>
        /// Reads a .bundle file.
        /// </summary>
        public void Read(string filename)
        {
            FileName = filename;
            Name = filename.Split('\\').Last();
            Items = new Dictionary<string, BundleItem>();

            using (var reader = new BinaryReader(new FileStream(FileName, FileMode.Open, FileAccess.Read)))
            {
                var idstring = reader.ReadBytes(IDString.Length);

                if (!IDString.SequenceEqual(idstring))
                {
                    throw new InvalidBundleException("Bundle header mismatch.");
                }

                bundlesize = reader.ReadUInt32();
                dummysize = reader.ReadUInt32();
                tocRealSize = reader.ReadUInt32();

                DataBlockOffset = tocRealSize + (uint)HEADER_SIZE;
                DataBlockSize = bundlesize - DataBlockOffset;

                reader.BaseStream.Seek(0x20, SeekOrigin.Begin);

                while (reader.BaseStream.Position < tocRealSize + 0x20)
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
                    ItemsList.Add(item);
                }


                reader.Close();
            }
        }

        /// <summary>
        /// Generate a bundle from a list of IWitcherFiles.
        /// </summary>
        /// <param name="Files"></param>
        public void Read(params IWitcherFile[] Files)
        {
            ItemsList = Files.ToList();

            // calculate ToC size
            dummysize = 0;
            tocRealSize = (UInt32)(ItemsList.Count * TOCEntrySize);

            // MAIN BODY
            int offset = GetOffset((int)tocRealSize);
            for (int i = 0; i < ItemsList.Count; i++)
            {
                IWitcherFile item = (IWitcherFile)ItemsList[i];
                //item.Bundle = this; //FIXME reparent items

                //get uncompressed data
                byte[] uc = item.UncompressedData();
                uncompressedData.Add(uc);

                //compress file
                List<byte> compressedFile = CompressFile(uc, (int)item.Compression).ToList();
                //padding
                var filesize = compressedFile.Count;
                var nextOffset = GetOffset(offset + filesize);
                int paddingLength = nextOffset - (offset + filesize);
                if (paddingLength > 0 && i < (ItemsList.Count - 1)) //don't pad the last item
                    compressedFile.AddRange(new byte[paddingLength]);

                BODY.Add(new KeyValuePair<uint, List<byte>>((uint)offset, compressedFile));

                offset = nextOffset;
            }
            bundlesize = (uint)offset;
            DataBlockOffset = tocRealSize + (uint)HEADER_SIZE;
            DataBlockSize = bundlesize - DataBlockOffset;
        }

        /// <summary>
        /// Serialize bundle to a .bundle file.
        /// </summary>
        /// <param name="outDir"></param>
        public void Write(string outDir)
        {
            var filePath = Path.Combine(outDir, Name);

            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {

                // Write Header
                bw.Write(IDString);
                bw.Write(bundlesize);
                bw.Write(dummysize);
                bw.Write(tocRealSize);
                bw.Write(new byte[] { 0x03, 0x00, 0x01, 0x00, 0x00, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13 }); //TODO: Figure out what the hell is this.

                // Write ToC
                var minDataOffset = ALIGNMENT_TARGET;
                for (int i = 0; i < ItemsList.Count; i++)
                {
                    IWitcherFile f = ItemsList[i];
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

        //FIXME buffers and blobs
        /// <summary>
        /// Create a .bundle file from a directory.
        /// </summary>
        /// <param name="outDir"></param>
        /// <param name="inDir"></param>
        public static void Pack(string inDir, string outDir)
        {
            List<IWitcherFile> bufferFiles = new List<IWitcherFile>();
            List<IWitcherFile> blobFiles = new List<IWitcherFile>();

            foreach (var f in Directory.EnumerateFiles(inDir, "*", SearchOption.AllDirectories))
            {
                var name = Encoding.Default.GetBytes(GetRelativePath(f, inDir)).ToArray();
                if (name.Length > 0x100)
                    name = name.Take(0x100).ToArray();
                if (name.Length < 0x100)
                    Array.Resize(ref name, 0x100);

                var bi = new BundleItem
                {
                    Name = System.Text.Encoding.Default.GetString(name),
                    Compression = 5, //FIXME make variable

                };

                //sort buffers out
                if (f.Split('\\').Last().Split('.').Last() == "buffer")
                    bufferFiles.Add(bi);
                else
                    blobFiles.Add(bi);
            }

            List<Bundle> bundles = new List<Bundle>();
            if (blobFiles.Count > 0)
                bundles.Add(new Bundle(blobFiles.ToArray()));
            if (bufferFiles.Count > 0)
                bundles.Add(new Bundle(bufferFiles.ToArray()));
            foreach (var b in bundles)
                b.Write(outDir);
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