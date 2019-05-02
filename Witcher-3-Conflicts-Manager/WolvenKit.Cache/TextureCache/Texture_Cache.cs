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
        //The images packed into this Texture cache file
        public List<TextureCacheItem> Files;

        public string TypeName => "TextureCache";
        public string FileName { get; set; }
        public List<uint> Chunkoffsets;
        public UInt64 Crc;
        public UInt32 UsedPages;
        public UInt32 EntryCount;
        public UInt32 StringTableSize;
        public UInt32 MipOffsetEntryCount;
        public UInt32 IDString;
        public UInt32 Version;
        public List<string> Names;

        public TextureCache()
        {
            Chunkoffsets = new List<uint>();
            Names = new List<string>();
            Files = new List<TextureCacheItem>();
        }

        public TextureCache(string filename)
        {
            this.Read(filename);
        }

        public void Read(string filepath)
        {
            try
            {
            FileName = filepath;
            Chunkoffsets = new List<uint>();
            using (var br = new BinaryReader(new FileStream(filepath, FileMode.Open)))
            {
                Files = new List<TextureCacheItem>();
                br.BaseStream.Seek(-32, SeekOrigin.End);
                Crc = br.ReadUInt64();
                UsedPages = br.ReadUInt32();
                EntryCount = br.ReadUInt32();
                StringTableSize = br.ReadUInt32();
                MipOffsetEntryCount = br.ReadUInt32();
                IDString = br.ReadUInt32();
                Version = br.ReadUInt32();
                //JMP to the top of the info table:
                //32 is the the size of the stuff we read so far.
                //Every entry has 52 bytes of info
                //The stringtable
                //Every offset is 4 bytes
                //The sum of this is how much we need to jump from the back
                var jmp = -(32 + (EntryCount * 52) + StringTableSize + (MipOffsetEntryCount * 4));
                br.BaseStream.Seek(jmp, SeekOrigin.End);
                var jmpoffset = br.BaseStream.Position;
                for (var i = 0; i < MipOffsetEntryCount; i++)
                {
                    Chunkoffsets.Add(br.ReadUInt32());
                }
                //BUG: "modW3EE\\content\\texture.cache" dies here! Investigate!!!!!!!!!!!!!
                /* wrong offset. 2032794, should be: 2109545 (= 2002944 + 0 + 106601)
                * for some reason, some entries are doubled in the (middle of the) stringtable of the texture.cache. 
                * leading to the string table being longer than it actually is (more entries than entrycount)
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
                //for (var i = 0; i < EntryCount; i++)
                var entrytableoffset = jmpoffset + (MipOffsetEntryCount * 4) + StringTableSize;
                var streampos = br.BaseStream.Position;
                while (streampos < entrytableoffset)
                {
                    
                    string entryname = br.ReadCR2WString();
                    if (!Names.Contains(entryname))
                        Names.Add(entryname);
                    streampos = br.BaseStream.Position;
                }

                //errorhandling -fuzzo
                if (br.BaseStream.Position != entrytableoffset)
                        throw new NotImplementedException();

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
                        PageOFfset = br.ReadInt32(),       
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
                    Files.Add(ti);
                }
                //dbg
                var footeroffset = br.BaseStream.Length - 32;
                if (br.BaseStream.Position != footeroffset)
                    throw new NotImplementedException();
                //dbg
                for (int i = 0; i < Files.Count; i++)
                {
                    TextureCacheItem t = Files[i];
                    br.BaseStream.Seek(t.PageOFfset * 4096, SeekOrigin.Begin);
                    t.ZSize = br.ReadUInt32(); //Compressed size
                    t.Size = br.ReadInt32(); //Uncompressed size
                    t.Part = br.ReadByte(); //maybe the 48bit part of OFFSET
                }
            }
            }
            catch (Exception e)
            {
                Debug.Assert(e != null);
            }
        }

        public static void Write(BinaryWriter bw)
        {
            //TODO: Finish this!
        }
    }
}
