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
    public class BundleItem : IWitcherFile
    {
        #region Info
        public string Name { get; set; }
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
        //public string FileName { get; set; }
        public byte[] CompressedBytes { get; set; }
        public string CompressionType
        {
            get
            {
                switch (Compression)
                {
                    case 0:
                        return "None";
                    case 1:
                        return "Zlib";
                    case 2:
                        return "Snappy";
                    case 3:
                        return "Doboz";
                    case 4:
                        return "Lz4";
                    case 5:
                        return "Lz4";
                    default:
                        return "Unknown";
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
            //FIXME properly handle this
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
            //FIXME 
            /*else if (File.Exists(FileName))
            {
                using (var file = MemoryMappedFile.CreateFromFile(FileName, FileMode.Open))
                {
                    using (var vs = file.CreateViewStream())
                    {
                        var buffer = new byte[vs.Length];
                        var c = vs.Read(buffer, 0, buffer.Length);
                        var compressed = LZ4.LZ4Codec.EncodeHC(buffer, 0, buffer.Length);

                        output.Write(compressed, 0, compressed.Length);
                    }
                }
            }*/
            else
            {
                if (CompressedBytes == null)
                {
                    //FIXME this would happen when the BundleItem was created from a file that was created in memory.
                    throw new InvalidBundleException("Found neither a bundle nor a file to read from.");
                }

                output.Write(CompressedBytes, 0, CompressedBytes.Length);
                
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
                        case "None":
                        {
                            viewstream.CopyTo(output);
                            break;
                        }
                        case "Lz4":
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = LZ4Codec.Decode(buffer, 0, c, (int) Size);
                            output.Write(uncompressed, 0, uncompressed.Length);
                            break;
                        }
                        case "Snappy":
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = SnappyCodec.Uncompress(buffer);
                            output.Write(uncompressed,0,uncompressed.Length);
                            break;
                        }
                        case "Doboz":
                        {
                            var buffer = new byte[ZSize];
                            var c = viewstream.Read(buffer, 0, buffer.Length);
                            var uncompressed = DobozCodec.Decode(buffer, 0, c);
                            output.Write(uncompressed, 0, uncompressed.Length);
                            break;
                        }
                        case "Zlib":
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
            var name = Encoding.Default.GetBytes(Name).ToArray();
            if (name.Length > 0x100)
                name = name.Take(0x100).ToArray();
            if (name.Length < 0x100)
                Array.Resize(ref name, 0x100);
            bw.Write(name); //Filename trimmed to 100 characters.

            bw.Write(Hash); //HASH
            bw.Write((UInt32)0x00000000); //EMPTY
            bw.Write((UInt32)Size); //SIZE
            bw.Write((UInt32)ZSize); //ZSIZE
            bw.Write((UInt32)PageOffset); //DATA OFFSET
            bw.Write((UInt32)0x00000000); //DATE
            bw.Write((UInt32)0x00000000); //TIME
            bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); //PADDING
            bw.Write((UInt32)CRC); //CRC32 FIXME: Check if the game actually cares.
            bw.Write((UInt32)Compression); // Compression.
        }

        #endregion
    }
}