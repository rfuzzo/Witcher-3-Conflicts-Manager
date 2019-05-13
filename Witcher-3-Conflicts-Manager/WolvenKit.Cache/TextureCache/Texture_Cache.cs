using DamienG.Security.Cryptography;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using W3Edit.Textures;
using WolvenKit.Common;
using WolvenKit.CR2W;
using WolvenKit.CR2W.Types;

namespace WolvenKit.Cache
{
    using Types;

    /// <summary>
    /// 
    /// TODO
    /// - reading image data when creating cache from file (high priority)
    /// - create mipmaps when creating cache from file (high priority)
    /// 
    /// - make Footer creation dynamic (low priority)
    /// 
    /// </summary>
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
        public string TypeName => "TextureCache";

        private TextureCacheFooter Footer;
        #endregion

        #region Properties
        public List<TextureCacheItem> Items { get; set; } = new List<TextureCacheItem>();
        public List<uint> MipsOffsets { get; set; } = new List<uint>();
        public string FileName { get; set; }
        public CompressionType Compression { get; set; } = CompressionType.LZ4HC;

        #endregion

        #region Constructors
        public TextureCache()
        {
        }
        /// <summary>
        /// Create TextureCache from a .cache file.
        /// </summary>
        /// <param name="filePath"></param>
        public TextureCache(string filePath) : base()
        {
            this.Read(filePath);
        }
        /// <summary>
        /// Create a TextureCache from files in a directory.
        /// </summary>
        /// <param name="dir"></param>
        public TextureCache(DirectoryInfo indir) : base()
        {
            Read(indir.GetFiles("*", SearchOption.AllDirectories));
        }
        /// <summary>
        /// Create TextureCache from a list of TextureCacheItems (compressed)
        /// </summary>
        /// <param name="_files"></param>
        public TextureCache(TextureCacheItem[] _files) : base()
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

                #region Body
                foreach (TextureCacheItem item in Items)
                {
                    //zsize, size and part
                    bw.Write((UInt32)item.CachedZSizeNoMips);
                    bw.Write((Int32)item.CachedSizeNoMips);
                    bw.Write((Byte)item.CachedMipsCount);

                    //write compressed file
                    using (var ms = new MemoryStream())
                    {
                        byte[] compressedFile = new byte[item.CachedZSizeNoMips];

                        item.GetCompressedFile(ms);

                        bw.Write(compressedFile);
                    }

                    

                    //write mips
                    if (item.CachedMipsCount > 0)
                    {
                        using (var ms = new MemoryStream())
                        {
                            item.WriteMipmaps(ms);
                            bw.Write(ms.ToArray());
                        }
                    }
                    

                    //pad body items
                    long nextOffset = GetOffset((int)bw.BaseStream.Position);
                    long paddingLength = nextOffset - (bw.BaseStream.Position);
                    if (paddingLength > 0)
                        bw.Write(new byte[paddingLength]);

                    
                }
                #endregion

                #region InfoTables
                long infoTableStartOffset = bw.BaseStream.Position;
                //write Mipslist
                foreach (uint o in MipsOffsets)
                {
                    bw.Write((UInt32)o);
                }

                //write Namestable
                foreach (var item in Items)
                {
                    bw.WriteCR2WString(item.DepotPath);
                }

                //write Infotable
                foreach (var item in Items)
                {
                    item.Write(bw);
                }

                //write footer
                if (Footer.Crc == 0) // 0 when cache is not parsed from file
                {
                    long footerStartOffset = bw.BaseStream.Position;
                    int stringtablelength = (int)(footerStartOffset - infoTableStartOffset);
                    using (var ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[stringtablelength];
                        bw.BaseStream.Position = infoTableStartOffset;
                        bw.BaseStream.CopyTo(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        ms.Read(buffer, 0, stringtablelength);

                        var crc = FNV1a.HashFNV1a64(buffer);

                        Footer.Crc = crc;
                    }
                }

                
                Footer.Write(bw);
                #endregion
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
            FileName = filepath;
            if (!File.Exists(filepath))
            {
                return;
            }


            using (var br = new BinaryReader(new FileStream(filepath, FileMode.Open)))
            {
                Items = new List<TextureCacheItem>();

                #region Footer
                br.BaseStream.Seek(-32, SeekOrigin.End);
                Footer = new TextureCacheFooter();
                Footer.Read(br);

                //errorhandling
                if (!IDString.SequenceEqual(Footer.IDString))
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
                var jmp = -(32 + (Footer.EntryCount * 52) + Footer.StringTableSize + (Footer.MipEntryCount * 4));
                br.BaseStream.Seek(jmp, SeekOrigin.End);
                var jmpoffset = br.BaseStream.Position;

                //Mips
                for (var i = 0; i < Footer.MipEntryCount; i++)
                {
                    MipsOffsets.Add(br.ReadUInt32());
                }

                //Names
                //BUG: "modW3EE\\content\\texture.cache" dies here! Investigate!!!!!!!!!!!!!
                /*
                * for some reason, some entries are doubled in the (middle of the) stringtable of the texture.cache. 
                * leading to the string table being longer than it should be (more entries than entrycount)
                * this in turn let's the for loop (which runs over entrycount) to stop in the middle of the string-table
                * As a twist, there are actually duplicate file NAMES (but different compressed files) inside the bob texture.cache
                * which breaks any way of properly solving the w3ee problem
                * FIX
                * - check for entrys and names count
                * - if they are different (in the case of w3ee) try to resolve the error by making the names distinct
                * - this works for w3ee but is not guaranteed to work in all cases.
                * - skip loading if that didnt resolve the names/entry count
                */
                var Names = new List<string>();
                var entrytableoffset = jmpoffset + (Footer.MipEntryCount * 4) + Footer.StringTableSize;
                while (br.BaseStream.Position < entrytableoffset)
                {
                    string entryname = br.ReadCR2WString();
                    Names.Add(entryname);
                }

                //errorhandling
                if (Footer.EntryCount != Names.Count)
                {
                    //try resolving the error
                    var resolvedNames = Names.Distinct().ToList();
                    if (Footer.EntryCount == resolvedNames.Count)
                        Names = resolvedNames;
                    else
                        throw new NotImplementedException();
                }

                //errorhandling

                //Entries
                br.BaseStream.Seek(entrytableoffset, SeekOrigin.Begin);
                for (var i = 0; i < Footer.EntryCount; i++)
                {
                    var ti = new TextureCacheItem(this)
                    {
                        DepotPath = Names[i],
                        ParentFile = FileName,
                        Hash = br.ReadInt32(),
                        /*-------------TextureCacheEntryBase---------------*/
                        PathStringIndex = br.ReadInt32(),
                        PageOffset = br.ReadInt32(), //NOTE: texturecache pointers are stored as pagenumber, while bundleitems store absolute offset -_-
                        ZSize = (uint)br.ReadInt32(),
                        Size = br.ReadInt32(),
                        BaseAlignment = br.ReadUInt32(),

                        BaseWidth = br.ReadUInt16(),
                        BaseHeight = br.ReadUInt16(),
                        TotalMipsCount = br.ReadUInt16(),
                        SliceCount = br.ReadUInt16(),

                        MipOffsetIndex = br.ReadInt32(),
                        MipsCount = br.ReadInt32(),
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
                    t.CachedZSizeNoMips = br.ReadUInt32(); //Compressed size
                    t.CachedSizeNoMips = br.ReadInt32(); //Uncompressed size
                    t.CachedMipsCount = br.ReadByte(); //mips count
                }
                #endregion
            }
        }

        /// <summary>
        /// Reads a list of TextureCacheItems. (compressed)
        /// </summary>
        /// <param name="_files"></param>
        private void Read(params TextureCacheItem[] _files)
        {
            //stringtable
            var stringTable = new Dictionary<int, int>();
            int stoffset = 0;
            var relMipsTable = new Dictionary<int, uint[]>();
            for (int i = 0; i < _files.Length; i++)
            {
                TextureCacheItem f = _files[i];
                stringTable.Add( i, stoffset);
                stoffset += f.DepotPath.Length + 1;

                //mipmaps
                relMipsTable.Add(i ,f.GetMipsOffsettable());
            }

            //entrytable
            long offset = 0;
            for (int i = 0; i < _files.Length; i++)
            {
                TextureCacheItem item = (TextureCacheItem)_files[i];
                long nextOffset = GetOffset(offset + (int)item.ZSize);

                //calculate mipoffset
                var mo = relMipsTable.Where(_ => _.Key < i).SelectMany(_ => _.Value).Count();
                var l = relMipsTable[i];
                for (int j = 0; j < relMipsTable[i].Count(); j++)
                    l[j] += item.CachedZSizeNoMips + 9;
                MipsOffsets.AddRange(l);

                if (offset / ALIGNMENT_TARGET < 0)
                {

                }

                TextureCacheItem newItem = new TextureCacheItem(item.Bundle)
                {
                    BundlePath = item.Bundle.FileName,

                    CachedMipsCount = item.CachedMipsCount,
                    CachedZSizeNoMips = item.CachedZSizeNoMips,
                    CachedSizeNoMips = item.CachedSizeNoMips,

                    DepotPath = item.DepotPath,
                    Hash = item.Hash,
                    /*-------------TextureCacheEntryBase---------------*/
                    PathStringIndex = stringTable[i],
                    PageOffset = offset / ALIGNMENT_TARGET,
                    ZSize = item.ZSize,
                    Size = item.Size,
                    BaseAlignment = item.BaseAlignment,
                    BaseWidth = item.BaseWidth,
                    BaseHeight = item.BaseHeight,
                    TotalMipsCount = item.TotalMipsCount,
                    SliceCount = item.SliceCount,
                    MipOffsetIndex = mo,
                    MipsCount = item.MipsCount,
                    TimeStamp = item.TimeStamp,
                    /*-------------TextureCacheEntryBase---------------*/
                    Type = item.Type,
                    IsCube = item.IsCube
                };
                Items.Add(newItem);

                offset = nextOffset;
            }            

            //create footer
            Footer = new TextureCacheFooter
            {
                Crc = 0,
                UsedPages = (UInt32)(offset / ALIGNMENT_TARGET),
                EntryCount = (UInt32)Items.Count,
                StringTableSize = (UInt32)stoffset,
                MipEntryCount = (uint)MipsOffsets.Count,
                IDString = IDString,
            };
        }

        /// <summary>
        /// Generate a bundle from a list of binary Files. (uncompressed)
        /// </summary>
        /// <param name="Files"></param>
        private void Read(FileInfo[] _files)
        {
            //stringtable
            var stringTable = new Dictionary<string, int>();
            int stoffset = 0;
            foreach (var f in _files)
            {
                stringTable.Add(GetRelativePath(f.FullName, f.Directory.FullName), stoffset);
                stoffset += f.Name.Length + 1;
            }

            long offset = 0;
            foreach (FileInfo f in _files)
            {
                long size;
                uint zSize;

                //get the raw bytes, rawbyte length and compressed bytes length
                using (var file = MemoryMappedFile.CreateFromFile(f.FullName, FileMode.Open))
                using (var vs = file.CreateViewStream(0, f.Length))
                {
                    var buffer = new byte[f.Length];
                    vs.Read(buffer, 0, buffer.Length);

                    size = buffer.Length;
                    byte[] compressed = GetCompressed(buffer);
                    zSize = (uint)compressed.Length;
                }

                //padding
                long nextOffset = GetOffset(offset + (int)zSize);

                TextureCacheItem newItem = new TextureCacheItem(this)
                {
                    DepotPath = GetRelativePath(f.FullName, f.Directory.FullName),
                    Hash = 0, //FIXME

                    /*-------------TextureCacheEntryBase---------------*/
                    PathStringIndex = stringTable[f.Name],
                    PageOffset = offset / ALIGNMENT_TARGET,
                    ZSize = zSize,
                    Size = size,
                    BaseAlignment = 0, //FIXME
                    BaseWidth = 0, //FIXME
                    BaseHeight = 0, //FIXME
                    TotalMipsCount = 0, //FIXME
                    SliceCount = 0, //FIXME
                    MipOffsetIndex = 0, //FIXME
                    MipsCount = 0, //FIXME
                    TimeStamp = 0, //FIXME
                    /*-------------TextureCacheEntryBase---------------*/
                    Type = 0, //FIXME
                    IsCube = 0, //FIXME

                };
                Items.Add(newItem);

                offset = nextOffset;
            }

            //create footer
            Footer = new TextureCacheFooter
            {
                Crc = 0, //NOTE this stays 0, crc will get created when writing
                UsedPages = (UInt32)(offset / ALIGNMENT_TARGET),
                EntryCount = (UInt32)Items.Count,
                StringTableSize = (UInt32)stoffset,
                MipEntryCount = 0, //FIXME
                IDString = IDString,
            };
        }

        //FIXME is it always Zlib?
        private byte[] GetCompressed(byte[] buffer)
        {
            switch (Compression)
            {
                case CompressionType.None:
                    return buffer;
                case CompressionType.ZLib:
                    
                case CompressionType.Snappy:
                    
                case CompressionType.Doboz:
                   
                case CompressionType.LZ4:
                   
                case CompressionType.LZ4HC:
                   
                default:
                    //return LZ4.LZ4Codec.EncodeHC(buffer, 0, buffer.Length);
                    return ZlibStream.CompressBuffer(buffer);
            }
        }

        /// <summary>
        /// Calculate the next possible alignment target from an offset.
        /// </summary>
        /// <param name="minPos"></param>
        /// <returns></returns>
        private static long GetOffset(long minPos)
        {
            long firstValidPos = (minPos / ALIGNMENT_TARGET) * ALIGNMENT_TARGET + ALIGNMENT_TARGET;
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
