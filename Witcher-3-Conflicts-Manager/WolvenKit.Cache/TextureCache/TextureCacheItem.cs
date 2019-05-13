using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zlib;
using W3Edit.Textures;
using WolvenKit.Common;
using WolvenKit.CR2W.Types;

namespace WolvenKit.Cache
{
    public class TextureCacheItem : IWitcherFile
    {
        #region Info
        public string DateString { get; set; }
        public string DepotPath { get; set; }
        public Int32 Hash;
        public Int32 PathStringIndex;
        public long PageOffset { get; set; }
        public UInt32 ZSize { get; set; } //compressed size with mipmaps
        public long Size { get; set; } //uncompressed size with mipmaps
        public UInt32 BaseAlignment;
        public UInt16 BaseWidth;
        public UInt16 BaseHeight;
        public UInt16 TotalMipsCount;
        public UInt16 SliceCount;
        public Int32 MipOffsetIndex;
        public Int32 MipsCount;
        public Int64 TimeStamp;
        public Int16 Type;
        public Int16 IsCube;
        #endregion

        #region Properties
        public string ParentFile;
        public IWitcherArchiveType Bundle { get; set; }


        public Byte CachedMipsCount { get; set; } //cached mipmaps count
        public UInt32 CachedZSizeNoMips { get; set; } //compressed size without mipmaps
        public Int32 CachedSizeNoMips { get; set; } //uncompressed size without mipmaps
        public CompressionType CompressionType => CompressionType.ZLib;

        public string BundlePath { get; set; }
        public string FilePath { get; set; }
        public int MipMapSize => (int)(ZSize - CachedZSizeNoMips - 9);

        public enum ETextureFormat
        {
            TEXFMT_A8 = 0x0,
            TEXFMT_A8_Scaleform = 0x1,
            TEXFMT_L8 = 0x2,
            TEXFMT_R8G8B8X8 = 0x3,
            TEXFMT_R8G8B8A8 = 0x4,
            TEXFMT_A8L8 = 0x5,
            TEXFMT_Uint_16_norm = 0x6,
            TEXFMT_Uint_16 = 0x7,
            TEXFMT_Uint_32 = 0x8,
            TEXFMT_R32G32_Uint = 0x9,
            TEXFMT_R16G16_Uint = 0xA,
            TEXFMT_Float_R10G10B10A2 = 0xB,
            TEXFMT_Float_R16G16B16A16 = 0xC,
            TEXFMT_Float_R11G11B10 = 0xD,
            TEXFMT_Float_R16G16 = 0xE,
            TEXFMT_Float_R32G32 = 0xF,
            TEXFMT_Float_R32G32B32A32 = 0x10,
            TEXFMT_Float_R32 = 0x11,
            TEXFMT_Float_R16 = 0x12,
            TEXFMT_D24S8 = 0x13,
            TEXFMT_D24FS8 = 0x14,
            TEXFMT_D32F = 0x15,
            TEXFMT_D16U = 0x16,
            TEXFMT_BC1 = 0x17,
            TEXFMT_BC2 = 0x18,
            TEXFMT_BC3 = 0x19,
            TEXFMT_BC4 = 0x1A,
            TEXFMT_BC5 = 0x1B,
            TEXFMT_BC6H = 0x1C,
            TEXFMT_BC7 = 0x1D,
            TEXFMT_R8_Uint = 0x1E,
            TEXFMT_NULL = 0x1F,
            TEXFMT_Max = 0x20,
        };
        public Dictionary<Int16, ETextureFormat> formats = new Dictionary<Int16, ETextureFormat>()
        {
            {0x3FD,ETextureFormat.TEXFMT_R8G8B8A8},
            {0x407,ETextureFormat.TEXFMT_BC1},
            {0x408,ETextureFormat.TEXFMT_BC3},
            {0x409, ETextureFormat.TEXFMT_BC6H},
            {0x40A, ETextureFormat.TEXFMT_BC7},
            {0x40B,ETextureFormat.TEXFMT_Float_R16G16B16A16},
            {0x40C,ETextureFormat.TEXFMT_Float_R32G32B32A32},
            {0x40D, ETextureFormat.TEXFMT_BC2},
            {0x40E, ETextureFormat.TEXFMT_BC4},
            {0x40F, ETextureFormat.TEXFMT_BC5}
        };
        #endregion

        #region Constructors
        public TextureCacheItem(IWitcherArchiveType parent)
        {
            Bundle = parent;
        }
        #endregion


        public void GetCompressedFile(Stream output)
        {
            if (File.Exists(Bundle.FileName) || File.Exists(BundlePath))
            {
                using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
                {
                    using (var viewstream = file.CreateViewStream((PageOffset * 4096) + 9, CachedZSizeNoMips, MemoryMappedFileAccess.Read))
                    {
                        viewstream.CopyTo(output);
                    }
                }
            }
            else if (File.Exists(FilePath))
            {
                using (var file = MemoryMappedFile.CreateFromFile(FilePath, FileMode.Open))
                {
                    using (var vs = file.CreateViewStream())
                    {
                        var buffer = new byte[vs.Length];
                        var c = vs.Read(buffer, 0, buffer.Length);
                        var compressed = LZ4.LZ4Codec.EncodeHC(buffer, 0, buffer.Length);

                        output.Write(compressed, 0, compressed.Length);
                    }
                }
            }
            else
            {
                throw new InvalidCacheException("Found neither a bundle nor a file to read from.");
            }
        }

        

        public void Extract(Stream output)
        {
            using (var file = MemoryMappedFile.CreateFromFile(this.ParentFile, FileMode.Open))
            {
                using (var viewstream = file.CreateViewStream((PageOffset * 4096) + 9, ZSize, MemoryMappedFileAccess.Read))
                {
                    //TODO: Finish this once we have a proper dds reader/writer
                    byte Dxt = BitConverter.GetBytes(Type)[0];
                    uint fmt = 0;
                    if (Dxt == 7) fmt = 1;
                    else if (Dxt == 8) fmt = 4;
                    else if (Dxt == 10) fmt = 4;
                    else if (Dxt == 13) fmt = 3;
                    else if (Dxt == 14) fmt = 6;
                    else if (Dxt == 15) fmt = 4;
                    else if (Dxt == 253) fmt = 0;
                    else if (Dxt == 0) fmt = 0;
                    else throw new Exception("Invalid image!");
                    var cubemap = (Type == 3 || Type == 0) && (SliceCount == 6);
                    uint depth = 0;
                    if (SliceCount > 1 && Type == 4) depth = SliceCount;
                    if (Type == 3 && Dxt == 253) BaseAlignment = 32;
                    var header = new DDSHeader().generate(
                            BaseWidth,
                            BaseHeight,
                            TotalMipsCount,
                            fmt,
                            BaseAlignment,
                            IsCube == 1,
                            depth)
                        .Concat(BitConverter.GetBytes((Int32)0)).ToArray();
                    output.Write(header, 0, header.Length);
                    if (!(SliceCount == 6 && (Type == 253 || Type == 0)))
                    {
                        using (var zs = new ZlibStream(viewstream, CompressionMode.Decompress))
                        {
                            zs.CopyTo(output);
                        }
                    }
                        
                }
            }
        }

        public void Extract(string filename)
        {
            using (var output = new FileStream(filename, FileMode.CreateNew, FileAccess.Write))
            {
                Extract(output);
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((Int32)Hash);
            bw.Write((Int32)PathStringIndex);
            bw.Write((Int32)PageOffset);
            bw.Write((Int32)ZSize);
            bw.Write((Int32)Size);
            bw.Write((Int32)BaseAlignment);
            bw.Write((UInt16)BaseWidth);
            bw.Write((UInt16)BaseHeight);
            bw.Write((UInt16)TotalMipsCount);
            bw.Write((UInt16)SliceCount);
            bw.Write((Int32)MipOffsetIndex);
            bw.Write((Int32)MipsCount);
            bw.Write((UInt64)TimeStamp);
            bw.Write((UInt16)Type);
            bw.Write((UInt16)IsCube);
        }

        /// <summary>
        /// Get the list of MipmapOffsets
        /// </summary>
        /// <returns></returns>
        internal uint[] GetMipsOffsettable()
        {
            if (File.Exists(Bundle.FileName))
            {
                var tempDict = new uint[MipsCount];
                var mipmapSectionOffset = (PageOffset * 4096) + 9 + CachedZSizeNoMips;
                int msize = MipMapSize;

                using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
                using (var vs = file.CreateViewStream(mipmapSectionOffset, msize, MemoryMappedFileAccess.Read))
                using (var br = new BinaryReader(vs))
                {
                    for (int i = 0; i < MipsCount; i++)
                    {
                        var relOffset = vs.Position;

                        //mipmap section header (9 bytes)
                        //length = (count * 256) + overflow
                        byte overflow = br.ReadByte(); //not sure how to call this. 1 byte of 
                        UInt32 mipPageCount = br.ReadUInt32(); //4bytes pagecount
                        UInt16 dim1 = br.ReadUInt16(); //2bytes dim1
                        UInt16 dim2 = br.ReadUInt16(); //2bytes dim2
                        var length = (mipPageCount * 256) + overflow;

                        tempDict[i] = (uint)relOffset;
                        br.BaseStream.Seek(length, SeekOrigin.Current);
                    }
                }
                return tempDict;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Writes the item's mipmaps to a stream.
        /// </summary>
        /// <param name="bw"></param>
        internal void WriteMipmaps(Stream output)
        {
            if (File.Exists(Bundle.FileName))
            {
                var mipmapSectionOffset = (PageOffset * 4096) + 9 + CachedZSizeNoMips;

                using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
                using (var vs = file.CreateViewStream(mipmapSectionOffset, MipMapSize, MemoryMappedFileAccess.Read))
                {
                    vs.CopyTo(output);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }


    }
}
