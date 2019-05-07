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

        private BundleHeader Header;

        #endregion

        #region Properties
        //FIXME make that dynamic
        public uint DataBlockSize {
            get
            {
                return Header.Bundlesize - DataBlockOffset;
            }
        }
        public uint DataBlockOffset {
            get
            {
                return Header.TocRealSize + (uint)HEADER_SIZE;
            }
        }
        public string TypeName => "Bundle";
        public string FileName { get; set; }
        public string Name { get; set; }
        //public Dictionary<string, BundleItem> Items { get; set; } //FIXME unused
        public List<BundleItem> ItemsList { get; set; } = new List<BundleItem>();
        #endregion

        #region Constructors
        public Bundle()
        {

        }
        /// <summary>
        /// Create Bundle from a .bundle file.
        /// </summary>
        /// <param name="filePath"></param>
        public Bundle(string filePath)
        {
            Read(filePath);
        }
        /// <summary>
        /// Create a bundle from a list of files in a directory.
        /// </summary>
        /// <param name="indir"></param>
        public Bundle(DirectoryInfo indir)
        {
            //handle buffers //FIXME

            Read(indir.GetFiles("*", SearchOption.AllDirectories));
        }
        /// <summary>
        /// Create Bundle from a list of BundleItems (compressed)
        /// </summary>
        /// <param name="_files"></param>
        public Bundle(BundleItem[] _files)
        {
            Read(_files);

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
        /// Create Bundle from a list of IWitcherFiles (uncompressed)
        /// </summary>
        /// <param name="_files"></param>
        public Bundle(IWitcherFile[] _files)
        {
            Read(_files);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Serialize bundle to a .bundle file.
        /// </summary>
        /// <param name="outdir"></param>
        public void Write(string outdir)
        {
            var filePath = Path.Combine(outdir, Name);

            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                // Write Header
                Header.Write(bw);
                //FIXME this is going to be static in the sense that 
                //one will not be able to add or remove items inside the bundle directly. 
                //Bundles have to be packed from a list of items once.

                // Write ToC
                var minDataOffset = ALIGNMENT_TARGET;
                foreach (BundleItem f in ItemsList)
                {
                    f.Write(bw);
                }
                //pad the ToC
                int writePosition = (int)bw.BaseStream.Position;
                int tocPadding = GetOffset(writePosition) - writePosition;
                if (tocPadding > 0)
                    bw.Write(new byte[tocPadding]);

                // Write Body
                for (int i = 0; i < ItemsList.Count; i++)
                {
                    BundleItem item = ItemsList[i];

                    //compressed file
                    var compressedFile = new List<byte>();
                    using (var ms = new MemoryStream())
                    {
                        item.GetCompressedFile(ms);
                        compressedFile.AddRange(ms.ToArray());
                    }

                    //pad body items
                    int filesize = (int)item.ZSize;
                    int nextOffset = GetOffset((int)item.PageOffset + filesize);
                    int paddingLength = nextOffset - ((int)item.PageOffset + filesize);
                    if (paddingLength > 0 && i < (ItemsList.Count - 1)) //don't pad the last item
                        compressedFile.AddRange(new byte[paddingLength]);

                    bw.Write(compressedFile.ToArray());
                }
            }
        }
        #endregion

        #region Private Methods 
        /// <summary>
        /// Reads a .bundle file.
        /// </summary>
        private void Read(string filename)
        {
            FileName = filename;
            Name = filename.Split('\\').Last();
            //Items = new Dictionary<string, BundleItem>();

            using (var br = new BinaryReader(new FileStream(FileName, FileMode.Open, FileAccess.Read)))
            {
                //read bundleheader
                Header = new BundleHeader();
                Header.Read(br);
                if (!IDString.SequenceEqual(Header.IDString))
                    throw new InvalidBundleException("Bundle header mismatch.");

                //Read Table of Contents
                br.BaseStream.Seek(0x20, SeekOrigin.Begin);
                while (br.BaseStream.Position < Header.TocRealSize + 0x20)
                {
                    var item = new BundleItem
                    {
                        Bundle = this
                    };

                    var strname = Encoding.Default.GetString(br.ReadBytes(0x100));

                    item.Name = strname.Substring(0, strname.IndexOf('\0'));
                    item.Hash = br.ReadBytes(16);
                    item.Empty = br.ReadUInt32();
                    item.Size = br.ReadUInt32();
                    item.ZSize = br.ReadUInt32();
                    item.PageOffset = br.ReadUInt32();

                    var date = br.ReadUInt32();
                    var y = date >> 20;
                    var m = date >> 15 & 0x1F;
                    var d = date >> 10 & 0x1F;

                    var time = br.ReadUInt32();
                    var h = time >> 22;
                    var n = time >> 16 & 0x3F;
                    var s = time >> 10 & 0x3F;

                    item.DateString = string.Format(" {0}/{1}/{2} {3}:{4}:{5}", d, m, y, h, n, s);

                    item.Zero = br.ReadBytes(16);    //00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 (always, in every archive)
                    item.CRC = br.ReadUInt32();    //CRC32 for the uncompressed data
                    item.Compression = br.ReadUInt32();

                    //Items.Add(item.Name, item);
                    ItemsList.Add(item);
                }


                br.Close();
            }
        }

        /// <summary>
        /// Generate a bundle from a list of BundleItems. (compressed)
        /// </summary>
        /// <param name="Files"></param>
        private void Read(BundleItem[] Files)
        {
            uint tocRealSize = (UInt32)(Files.Length * TOCEntrySize);

            // MAIN BODY
            int offset = GetOffset((int)tocRealSize);
            foreach (BundleItem item in Files)
            {
                int nextOffset = GetOffset(offset + (int)item.ZSize);

                BundleItem newItem = new BundleItem()
                {
                    Name = item.Name,
                    Hash = item.Hash, //FIXME?
                    Size = item.Size,
                    ZSize = item.ZSize,
                    Compression = item.Compression,
                    DateString = item.DateString,
                    Bundle = this, //FIXME? this will create problems with accessing compressed data from Memorymappedfiles
                    PageOffset = (uint)offset,
                    CRC = item.CRC //CRC = Crc32C.Crc32CAlgorithm.Compute(compressedFile.ToArray()) //FIXME is that crc over the compressed or uncompressed bytes?
                };
                ItemsList.Add(newItem);

                offset = nextOffset;
            }

            //create header
            uint dummysize = 0;
            uint bundlesize = (uint)offset;
            Header = new BundleHeader(IDString, bundlesize, dummysize, tocRealSize);
        }

        /// <summary>
        /// Generate a bundle from a list of IWitcherFiles. (uncompressed)
        /// </summary>
        /// <param name="Files"></param>
        private void Read(IWitcherFile[] Files)
        {
            uint tocRealSize = (UInt32)(Files.Length * TOCEntrySize);

            int offset = GetOffset((int)tocRealSize);
            foreach (IWitcherFile f in Files)
            {
                //get the raw bytes
                var rawbytes = new byte[8]; //FIXME 
                var compressedBytes = LZ4.LZ4Codec.EncodeHC(rawbytes, 0, rawbytes.Length); //FIXME

                //padding
                int nextOffset = GetOffset(offset + compressedBytes.Length);

                BundleItem newItem = new BundleItem()
                {
                    CompressedBytes = compressedBytes,
                    //FileName = f.FullName,

                    Name = f.Name, //FIXME I need the relative path here
                    Hash = new byte[16], //FIXME
                    Size = rawbytes.Length,
                    ZSize = (uint)compressedBytes.Length,
                    Compression = 5, //Fixme where do we get the compression from?
                    DateString = "", //unused
                    Bundle = this,
                    PageOffset = (uint)offset,
                    CRC = Crc32C.Crc32CAlgorithm.Compute(rawbytes) //FIXME is that crc over the compressed or uncompressed bytes?
                };
                ItemsList.Add(newItem);

                offset = nextOffset;
            }

            //create header
            uint dummysize = 0;
            uint bundlesize = (uint)offset;
            Header = new BundleHeader(IDString, bundlesize, dummysize, tocRealSize);
        }

        /// <summary>
        /// Generate a bundle from a list of binary Files. (uncompressed)
        /// </summary>
        /// <param name="Files"></param>
        private void Read(FileInfo[] Files )
        {
            uint tocRealSize = (UInt32)(Files.Length * TOCEntrySize);

            int offset = GetOffset((int)tocRealSize);
            foreach (FileInfo f in Files)
            {
                //get the raw bytes
                var rawbytes = File.ReadAllBytes(f.FullName);
                var compressedBytes = LZ4.LZ4Codec.EncodeHC(rawbytes, 0, rawbytes.Length); //FIXME

                //padding
                int nextOffset = GetOffset(offset + compressedBytes.Length);

                BundleItem newItem = new BundleItem()
                {
                    CompressedBytes = compressedBytes,
                    //FileName = f.FullName,

                    Name = GetRelativePath(f.FullName, f.Directory.FullName), //FIXME I need the relative path here, so, the foldername is probably the modDir
                    Hash = new byte[16], //FIXME
                    Size = rawbytes.Length,
                    ZSize = (uint)compressedBytes.Length,
                    Compression = 5, //Fixme where do we get the compression from?
                    DateString = "", //unused
                    Bundle = this,
                    PageOffset = (uint)offset,
                    CRC = Crc32C.Crc32CAlgorithm.Compute(rawbytes) //FIXME is that crc over the compressed or uncompressed bytes?
                };
                ItemsList.Add(newItem);

                offset = nextOffset;
            }

            //create header
            uint dummysize = 0;
            uint bundlesize = (uint)offset;
            Header = new BundleHeader(IDString, bundlesize, dummysize, tocRealSize);
        }

        /// <summary>
        /// Calculate the next possible alignment target from an offset.
        /// </summary>
        /// <param name="minPos"></param>
        /// <returns></returns>
        private static int GetOffset(int minPos)
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
        private static string GetRelativePath(string filespec, string folder)
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

        private static int WriteCompressedData(BinaryWriter bw, byte[] Data, int ComType)
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
                        bw.Write(LZ4.LZ4Codec.EncodeHC(Data, 0, Data.Length));
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

        #endregion

    }


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