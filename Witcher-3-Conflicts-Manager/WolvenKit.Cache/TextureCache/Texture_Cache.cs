using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using W3Edit.Textures;
using WolvenKit.Common;
using WolvenKit.CR2W;
using WolvenKit.CR2W.Types;

namespace WolvenKit.Cache
{
    public class TextureCache : IWitcherArchiveType
    {
        #region Info
        private static readonly byte[] IDString =
        {
            (byte) 'H', (byte) 'C', (byte) 'X', (byte) 'T'
        };

        private static int FOOTER_SIZE = 32;
        private static int ALIGNMENT_TARGET = 4096;
        private static int TOCEntrySize = 52; //Size of a TOC Entry.

        private TextureCacheHeader Footer;
        #endregion

        #region Properties
        public List<TextureCacheItem> Items;

        public string TypeName => "TextureCache";
        public string FileName { get; set; }

        public List<uint> Chunkoffsets;
        public UInt64 Crc;
        public UInt32 UsedPages;
        public UInt32 EntryCount;
        public UInt32 StringTableSize;
        private UInt32 MipEntryCount;
        public UInt32 Version;
        public List<string> Names;

        #endregion

        #region Constructors
        public TextureCache()
        {
            Chunkoffsets = new List<uint>();
            Names = new List<string>();
            Items = new List<TextureCacheItem>();
        }
        /// <summary>
        /// Create TextureCache from a .cache file.
        /// </summary>
        /// <param name="filePath"></param>
        public TextureCache(string filePath)
        {
            this.Read(filePath);
        }
        /// <summary>
        /// Create a TextureCache from files in a directory.
        /// </summary>
        /// <param name="dir"></param>
        public TextureCache(DirectoryInfo dir)
        {
            Read(indir.GetFiles("*", SearchOption.AllDirectories));
        }
        /// <summary>
        /// Create TextureCache from a list of TextureCacheItems (compressed)
        /// </summary>
        /// <param name="_files"></param>
        public TextureCache(TextureCacheItem[] _files)
        {
            Read(_files);
        }
        /// <summary>
        /// Create TextureCache from a list of IWitcherFiles (uncompressed)
        /// </summary>
        /// <param name="_files"></param>
        public TextureCache(IWitcherFile[] _files)
        {
            Read(_files);
        }
        #endregion


        #region Public Methods
        /// <summary>
        /// serialize the TextureCache to a texture.cache file.
        /// </summary>
        /// <param name="outdir"></param>
        public void Write(string outdir)
        {
            var filePath = Path.Combine(outdir, "texture.cache");

            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                // Write Body
                foreach (TextureCacheItem item in Items)
                {
                    //zsize, size and part
                    bw.Write(item.ZSize);
                    bw.Write(item.Size);
                    bw.Write(item.Part);

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
                    if (paddingLength > 0 )
                        compressedFile.AddRange(new byte[paddingLength]);

                    bw.Write(compressedFile.ToArray());
                }


                //write Mipslist
                for (int i = 0; i < MipEntryCount; i++) //FIXME this should be dynamic I guess
                {
                    throw new NotImplementedException();
                }

                //write Namestable
                foreach (var item in Items)
                {
                    bw.WriteCR2WString(item.Name);
                }

                //write Infotable
                foreach (var item in Items)
                {
                    item.Write(bw);
                }

                //write footer //FIXME make dynamic
                Footer.Write(bw);
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Reads a texture.cache file.
        /// </summary>
        /// <param name="filepath"></param>
        private void Read(string filepath)
        {
            try
            {
                FileName = filepath;
                Chunkoffsets = new List<uint>();
                using (var br = new BinaryReader(new FileStream(filepath, FileMode.Open)))
                {
                    Items = new List<TextureCacheItem>();

                    #region Footer
                    br.BaseStream.Seek(-32, SeekOrigin.End);
                    Crc = br.ReadUInt64();
                    UsedPages = br.ReadUInt32();
                    EntryCount = br.ReadUInt32();
                    StringTableSize = br.ReadUInt32();
                    MipEntryCount = br.ReadUInt32();
                    var _IDString = br.ReadBytes(IDString.Length);
                    Version = br.ReadUInt32();

                    //errorhandling
                    if (!IDString.SequenceEqual(_IDString))
                        throw new InvalidCacheException("Cache header mismatch.");
                    //errorhandling

                    #endregion

                    #region InfoTable
                    //JMP to the top of the info table:
                    //32 is the the size of the stuff we read so far.
                    //Every entry has 52 bytes of info
                    //The stringtable
                    //Every offset is 4 bytes
                    //The sum of this is how much we need to jump from the back
                    var jmp = -(32 + (EntryCount * 52) + StringTableSize + (MipEntryCount * 4));
                    br.BaseStream.Seek(jmp, SeekOrigin.End);
                    var jmpoffset = br.BaseStream.Position;

                    //Mips
                    for (var i = 0; i < MipEntryCount; i++)
                    {
                        Chunkoffsets.Add(br.ReadUInt32());
                    }

                    //Names
                    //BUG: "modW3EE\\content\\texture.cache" dies here! Investigate!!!!!!!!!!!!!
                    /*
                    * for some reason, some entries are doubled in the (middle of the) stringtable of the texture.cache. 
                    * leading to the string table being longer than it should be (more entries than entrycount)
                    * this in turn let's the for loop (which runs over entrycount) to stop in the middle of the string-table
                    * FIX: 
                    * 1 force jump to the end of the stringtable (we know the real end since the footer has it)
                    * -- but this is problematic since we didn't read all names properly (some are doubled and the rest is missing)
                    * 2 check for already existing entries in the nameArray and only add new
                    * - this *could* lead to a missmatch between namesCount and entrycount?
                    * -- check table offset in the end and throw if different 
                    * - we would need a while-loop since the ReadCR2WString read and the entrycount are different
                    * -- prone to error, have some error handling with br.streampos
                    * - not at all clear how the duplicate names correspond to the entrytable
                    * -- TODO
                    * -fuzzo
                    */
                    Names = new List<string>();
                    var entrytableoffset = jmpoffset + (MipEntryCount * 4) + StringTableSize;
                    while (br.BaseStream.Position < entrytableoffset)
                    {
                        string entryname = br.ReadCR2WString();
                        if (!Names.Contains(entryname))
                            Names.Add(entryname);
                    }

                    //errorhandling
                    if (br.BaseStream.Position != entrytableoffset)
                        throw new NotImplementedException();
                    //errorhandling

                    //Entries
                    br.BaseStream.Seek(entrytableoffset, SeekOrigin.Begin);
                    for (var i = 0; i < EntryCount; i++)
                    {
                        var ti = new TextureCacheItem(this)
                        {
                            Name = Names[i],
                            ParentFile = FileName,
                            Hash = br.ReadInt32(),
                            /*-------------TextureCacheEntryBase---------------*/
                            PathStringIndex = br.ReadInt32(),
                            PageOffset = br.ReadInt32(),
                            CompressedSize = br.ReadInt32(),
                            UncompressedSize = br.ReadInt32(),
                            BaseAlignment = br.ReadUInt32(),
                            BaseWidth = br.ReadUInt16(),
                            BaseHeight = br.ReadUInt16(),
                            Mipcount = br.ReadUInt16(),
                            SliceCount = br.ReadUInt16(),
                            MipOffsetIndex = br.ReadInt32(),
                            NumMipOffsets = br.ReadInt32(),
                            TimeStamp = br.ReadInt64(),
                            /*-------------TextureCacheEntryBase---------------*/
                            Type = br.ReadInt16(),
                            IsCube = br.ReadInt16()
                        };
                        Items.Add(ti);
                    }
                    #endregion

                    #region Data
                    //errorhandling
                    var footeroffset = br.BaseStream.Length - 32;
                    if (br.BaseStream.Position != footeroffset)
                        throw new NotImplementedException();
                    //errorhandling

                    for (int i = 0; i < Items.Count; i++)
                    {
                        TextureCacheItem t = Items[i];
                        br.BaseStream.Seek(t.PageOffset * 4096, SeekOrigin.Begin);
                        t.ZSize = br.ReadUInt32(); //Compressed size
                        t.Size = br.ReadInt32(); //Uncompressed size
                        t.Part = br.ReadByte(); //maybe the 48bit part of OFFSET
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                Debug.Assert(e != null);
            }
        }
        /// <summary>
        /// Reads a list of TextureCacheItems.
        /// </summary>
        /// <param name="_files"></param>
        private void Read(params TextureCacheItem[] _files)
        {
            foreach (var f in _files)
            {

                //compressed file
                var compressedFile = new List<byte>();
                using (var ms = new MemoryStream())
                {
                    f.GetCompressedFile(ms);
                    compressedFile.AddRange(ms.ToArray());
                }
                //List<byte> compressedFile = CompressFile(uc, (int)((BundleItem)item).Compression).ToList();
                //padding
                int filesize = (int)f.ZSize;
                int offset = compressedFile.Count;
                int nextOffset = GetOffset(offset + filesize);
                int paddingLength = nextOffset - (offset + filesize);
                if (paddingLength > 0 && i < (Items.Count - 1)) //don't pad the last item
                    compressedFile.AddRange(new byte[paddingLength]);




            }







            //construct entrytable

            //construct body

            //construct footer
            EntryCount = (UInt32)Items.Count;
            Version = (UInt32)6;
            Crc = 0;
            UsedPages = 0;
            StringTableSize = 0;
            MipEntryCount = 0;
        }

        private void Read(params IWitcherFile[] _files)
        {

        }



        private static int GetOffset(int minPos)
        {
            int firstValidPos = (minPos / ALIGNMENT_TARGET) * ALIGNMENT_TARGET + ALIGNMENT_TARGET;
            while (firstValidPos < minPos)
            {
                firstValidPos += ALIGNMENT_TARGET;
            }
            return firstValidPos;
        }
        #endregion






    }

    public class InvalidCacheException : Exception
    {
        public InvalidCacheException(string message) : base(message)
        {
        }
    }

    public class TextureCacheHeader
    {
        public byte[] IDString;
        public uint Bundlesize;
        private uint Dummysize;
        public uint TocRealSize;

        public TextureCacheHeader()
        {

        }

        public TextureCacheHeader(byte[] idstring, uint bundlesize, uint dummysize, uint tocrealsize)
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
