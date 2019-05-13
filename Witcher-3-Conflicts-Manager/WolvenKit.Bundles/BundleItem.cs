using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using Doboz;
using Ionic.Zlib;
using LZ4;
using Snappy;
using WolvenKit.Common;

namespace WolvenKit.Bundles
{
    using Types;
    /// <summary>
    /// 
    /// The BundleItem Class represents at the base an entry in the EntryTable of a .bundle file (region Info)
    /// It is therefore simply a collectionof info about the file that is compressed inside the bundle.
    /// Additionally it stores data about *where* inside the bundle the compressed bytes are found
    /// but should *NOT* store any actual bytesarray 
    /// 
    /// To keep this purpose of an InfoEntry, any logic *on* the actual compressed bytes should be done via filaccesors,
    /// that is either
    /// 1. with the physical .bundle file (and the offsets specified in here), or
    /// 2. with the physical cr2w file
    /// 
    /// PROBLEMS:
    /// - Depending on the number of times we want to perform logic on the bytes, this might actually be worse than keeping the bytes in memory. 
    /// Ideally this should only be done once, when packing. 
    /// 
    /// </summary>
    public class BundleItem : IWitcherFile
    {
        #region Info
        public string DepotPath { get; set; }
        public byte[] Hash { get; set; }
        public uint Empty { get; set; }
        public long Size { get; set; }
        public uint ZSize { get; set; }
        public long PageOffset { get; set; }
        public ulong TimeStamp { get; set; }
        public byte[] Zero { get; set; }
        public uint CRC { get; set; }
        public uint Compression { get; set; }
        public string DateString { get; set; }
        #endregion

        #region Properties
        public IWitcherArchiveType Bundle { get; set; }
        public IWitcherFileAccessor FileAccessor { get; set; }

        public CompressionType CompressionType
        {
            get
            {
                switch (Compression)
                {
                    case 0:
                        return CompressionType.None;
                    case 1:
                        return CompressionType.ZLib;
                    case 2:
                        return CompressionType.Snappy;
                    case 3:
                        return CompressionType.Doboz;
                    case 4:
                        return CompressionType.LZ4;
                    case 5:
                        return CompressionType.LZ4HC;
                    default:
                        return CompressionType.LZ4HC;
                }
            }
        }
        #endregion

        #region Constructors
        public BundleItem()
        {
            Empty = (UInt32)0x00000000;
            TimeStamp = (ulong)0x0000000000000000;

        }
        #endregion

        #region Public Methods


        public void GetCompressedFile(Stream output)
        {
            // To avoid memory issues, we want to prioritize reading from physical files
            // 1. first, we check if the bundle exists 
            // (that is so when the CBundle was parsed, i.e created from a real .bundle file 
            // or file have been reparented and the real bundle still exists)
            // 2. if that is not the case, then we check if the bundleitem was created from a real file 
            // ( that happens when the bundle has not been parsed but created from real files on the disk)

            if (File.Exists(Bundle.FileName))
            {
                using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
                {
                    using (var viewstream = file.CreateViewStream(PageOffset, ZSize, MemoryMappedFileAccess.Read))
                    {
                        viewstream.CopyTo(output);
                    }
                }
            }
            else if (FileAccessor != null)
            {
                if (FileAccessor is BundleAccesor && File.Exists(FileAccessor.Path))
                {
                    using (var file = MemoryMappedFile.CreateFromFile(FileAccessor.Path, FileMode.Open))
                    {
                        using (var viewstream = file.CreateViewStream(FileAccessor.Offset, ZSize, MemoryMappedFileAccess.Read))
                        {
                            viewstream.CopyTo(output);
                        }
                    }
                }
                else if (FileAccessor is FileAccessor && File.Exists(FileAccessor.Path))
                {
                    using (var file = MemoryMappedFile.CreateFromFile(FileAccessor.Path, FileMode.Open))
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
            }
            else
            {
                throw new InvalidBundleException("Found neither a bundle nor a file to read from.");
            }
        }

        public void Extract(Stream output)
        {
            using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
            {
                using (var viewstream = file.CreateViewStream(PageOffset, ZSize, MemoryMappedFileAccess.Read))
                {
                    switch (CompressionType)
                    {
                        case CompressionType.None:
                            {
                            viewstream.CopyTo(output);
                            break;
                        }
                        case CompressionType.LZ4:
                            {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = LZ4Codec.Decode(buffer, 0, c, (int) Size);
                            output.Write(uncompressed, 0, uncompressed.Length);
                            break;
                        }
                        case CompressionType.LZ4HC:
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = LZ4Codec.Decode(buffer, 0, c, (int) Size);
                            output.Write(uncompressed, 0, uncompressed.Length);
                            break;
                        }
                        case CompressionType.Snappy:
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = SnappyCodec.Uncompress(buffer);
                            output.Write(uncompressed,0,uncompressed.Length);
                            break;
                        }
                        case CompressionType.Doboz:
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = DobozCodec.Decode(buffer, 0, c);
                            output.Write(uncompressed, 0, uncompressed.Length);
                            break;
                        }
                        case CompressionType.ZLib:
                        {
                            var zlib = new ZlibStream(viewstream, CompressionMode.Decompress);
                            zlib.CopyTo(output);
                            break;
                        }
                        default:
                            throw new MissingCompressionException("Unhandled compression algorithm.")
                            {
                                Compression = Compression
                            };
                    }

                    viewstream.Close();
                }
            }
        }

        public void Extract(string filename)
        {
            using (var output = new FileStream(filename, FileMode.CreateNew, FileAccess.Write))
            {
                Extract(output);
                output.Close();
            }
        }

        public void Write(BinaryWriter bw)
        {
            var name = Encoding.Default.GetBytes(DepotPath).ToArray();
            if (name.Length > 0x100)
                name = name.Take(0x100).ToArray();
            if (name.Length < 0x100)
                Array.Resize(ref name, 0x100);
            bw.Write(name); //Filename trimmed to 100 characters.

            bw.Write(Hash);
            bw.Write((UInt32)0x00000000); //EMPTY
            bw.Write((UInt32)Size);
            bw.Write((UInt32)ZSize);
            bw.Write((UInt32)PageOffset);
            bw.Write((UInt32)0x00000000); //DATE
            bw.Write((UInt32)0x00000000); //TIME
            bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); //PADDING
            bw.Write((UInt32)CRC);
            bw.Write((UInt32)Compression);
        }

        #endregion
    }
}