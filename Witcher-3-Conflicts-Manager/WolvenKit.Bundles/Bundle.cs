using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WolvenKit.Common;
using System.IO.MemoryMappedFiles;

namespace WolvenKit.Bundles
{
    using Doboz;
    using Ionic.Zlib;
    using Snappy;
    using Types;

    /// <summary>
    /// 
    /// TODO
    /// 
    /// 
    /// 
    /// 
    /// - Make Header creation dynamic when writing the Bundle (low priority)
    /// - Figure out hashes for items and bundle (kow priority)
    /// 
    /// </summary>
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
        #endregion

        #region Properties
        public BundleHeader Header { get; set; }

        public string TypeName => "Bundle";
        public string FileName { get; set; }
        public string Name { get; set; }
        public List<BundleItem> Items { get; set; } = new List<BundleItem>();
        public CompressionType Compression { get; set; } = CompressionType.LZ4HC;

       

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
        /// Create a bundle from a list of files;
        /// </summary>
        /// <param name="moddir"></param>
        public Bundle(FileInfo[] filelist, DirectoryInfo moddir)
        {
            Read(filelist, moddir);

            //check for buffers
            var bufferCount = Items.Where(_ => _.DepotPath.Split('\\').Last().Split('.').Last() == "buffer").ToList().Count;
            if (bufferCount > 0)
            {
                if (bufferCount == Items.Count)
                    Name = "buffers.bundle";
                else
                    throw new InvalidBundleException("Buffers and files mixed in one bundle.");
            }
            else
                Name = "blob0.bundle";
        }
        /// <summary>
        /// Create Bundle from a list of BundleItems (compressed)
        /// </summary>
        /// <param name="_files"></param>
        public Bundle(BundleItem[] _files)
        {
            Read(_files);

            //check for buffers
            var bufferCount = Items.Where(_ => _.DepotPath.Split('\\').Last().Split('.').Last() == "buffer").ToList().Count;
            if (bufferCount > 0)
            {
                if (bufferCount == Items.Count)
                    Name = "buffers.bundle";
                else
                    throw new InvalidBundleException("Buffers and files mixed in one bundle.");
            }
            else
                Name = "blob0.bundle";
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

                // Write ToC
                var minDataOffset = ALIGNMENT_TARGET;
                foreach (BundleItem f in Items)
                {
                    f.Write(bw);
                }
                //pad the ToC
                int writePosition = (int)bw.BaseStream.Position;
                int tocPadding = GetOffset(writePosition) - writePosition;
                if (tocPadding > 0)
                    bw.Write(new byte[tocPadding]);

                // Write Body
                for (int i = 0; i < Items.Count; i++)
                {
                    BundleItem item = Items[i];

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
                    if (paddingLength > 0 && i < (Items.Count - 1)) //don't pad the last item
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

                    item.DepotPath = strname.Substring(0, strname.IndexOf('\0'));
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
                    item.CRC = br.ReadUInt32(); 
                    item.Compression = br.ReadUInt32();

                    Items.Add(item);
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
            int offset = GetOffset((int)tocRealSize + HEADER_SIZE);
            foreach (BundleItem item in Files)
            {
                int nextOffset = GetOffset(offset + (int)item.ZSize);

                BundleItem newItem = new BundleItem()
                {
                    FileAccessor = new BundleAccesor(item.Bundle.FileName, item.PageOffset),

                    DepotPath = item.DepotPath,
                    Hash = item.Hash,
                    Size = item.Size,
                    ZSize = item.ZSize,
                    Compression = item.Compression,
                    DateString = item.DateString,
                    Bundle = this,
                    PageOffset = (uint)offset,
                    CRC = item.CRC
                };
                Items.Add(newItem);

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
        private void Read(FileInfo[] Files, DirectoryInfo indir)
        {
            uint tocRealSize = (UInt32)(Files.Length * TOCEntrySize);

            int offset = GetOffset((int)tocRealSize + HEADER_SIZE);
            foreach (FileInfo f in Files)
            {
                

                long size;
                uint zSize;
                uint crc32;
                string relName = GetRelativePath(f.FullName, indir.FullName);

                //get the raw bytes, rawbyte length and compressed bytes length
                using (var file = MemoryMappedFile.CreateFromFile(f.FullName, FileMode.Open))
                using (var vs = file.CreateViewStream(0,f.Length))
                {
                    var buffer = new byte[f.Length];
                    vs.Read(buffer, 0, buffer.Length);

                    crc32 = Force.Crc32.Crc32Algorithm.Compute(buffer);

                    size = buffer.Length;
                    byte[] compressed = GetCompressed(buffer);
                    zSize = (uint)compressed.Length;
                }

               


                //padding
                int nextOffset = GetOffset(offset + (int)zSize);
                var hash = new byte[16]; //NOTE these are empty for mods, but are not empty in the vanilla bundles. leave empty for now

                BundleItem newItem = new BundleItem()
                {
                    FileAccessor = new FileAccessor(f.FullName),

                    DepotPath = relName,
                    Hash = hash,
                    Size = size,
                    ZSize = zSize,
                    Compression = (uint)Compression,
                    DateString = "", //unused
                    Bundle = this,
                    PageOffset = (uint)offset,
                    CRC = crc32
                };
                Items.Add(newItem);

                offset = nextOffset;
            }

            //create header
            uint dummysize = 0;
            uint bundlesize = (uint)offset;
            Header = new BundleHeader(IDString, bundlesize, dummysize, tocRealSize);
        }

        private byte[] GetCompressed(byte[] buffer)
        {
            switch (Compression)
            {
                case CompressionType.None:
                    return buffer;
                case CompressionType.ZLib:
                    return ZlibStream.CompressBuffer(buffer);
                case CompressionType.Snappy:
                    return SnappyCodec.Compress(buffer);
                case CompressionType.Doboz:
                    return DobozCodec.Encode(buffer, 0, buffer.Length);
                case CompressionType.LZ4:
                    return LZ4.LZ4Codec.Encode(buffer, 0, buffer.Length);
                case CompressionType.LZ4HC:
                    return LZ4.LZ4Codec.EncodeHC(buffer, 0, buffer.Length);
                default:
                    throw new MissingCompressionException("Unhandled compression algorithm.")
                    {
                        
                    };
            }
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


        #endregion

    }



}