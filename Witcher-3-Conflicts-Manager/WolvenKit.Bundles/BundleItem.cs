using System;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        public IWitcherArchiveType Bundle { get; set; }
        public string Name { get; set; }
        public byte[] Hash { get; set; }
        public uint Empty { get; set; }
        public long Size { get; set; }
        public uint ZSize { get; set; }
        public long PageOFfset { get; set; }
        public ulong TimeStamp { get; set; }
        public byte[] Zero { get; set; }
        public uint CRC { get; set; }
        public uint Compression { get; set; }
        public string DateString { get; set; }

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

        public byte[] UncompressedData()
        {
            using (MemoryMappedFile file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
            {
                using (MemoryMappedViewStream viewstream = file.CreateViewStream(PageOFfset, ZSize, MemoryMappedFileAccess.Read))
                {
                    switch (CompressionType)
                    {
                        case "None":
                            {
                                using (MemoryStream destination = new MemoryStream())
                                {
                                    viewstream.CopyTo(destination);
                                    return destination.ToArray();
                                }
                            }
                        case "Lz4":
                            {
                                var buffer = new byte[ZSize];
                                var c = viewstream.Read(buffer, 0, buffer.Length);
                                var uncompressed = LZ4Codec.Decode(buffer, 0, c, (int)Size);
                                return uncompressed;
                            }
                        case "Snappy":
                            {
                                var buffer = new byte[ZSize];
                                var c = viewstream.Read(buffer, 0, buffer.Length);
                                var uncompressed = SnappyCodec.Uncompress(buffer);
                                return uncompressed;
                            }
                        case "Doboz":
                            {
                                var buffer = new byte[ZSize];
                                var c = viewstream.Read(buffer, 0, buffer.Length);
                                var uncompressed = DobozCodec.Decode(buffer, 0, c);
                                return uncompressed;
                            }
                        case "Zlib":
                            {
                                using (MemoryStream destination = new MemoryStream())
                                {
                                    var zlib = new ZlibStream(viewstream, CompressionMode.Decompress);
                                    zlib.CopyTo(destination);
                                    return destination.ToArray();
                                }
                            }
                        default:
                            throw new MissingCompressionException("Unhandled compression algorithm.")
                            {
                                Compression = Compression
                            };
                    }
                }
            }
        }

        public void Extract(Stream output)
        {
            using (var file = MemoryMappedFile.CreateFromFile(Bundle.FileName, FileMode.Open))
            {
                using (var viewstream = file.CreateViewStream(PageOFfset, ZSize, MemoryMappedFileAccess.Read))
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
        public BundleItem()
        {

        }

        public BundleItem(string filePath)
        {
            Hash = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Empty = (UInt32)0x00000000;
            Size = (UInt32)new FileInfo(filePath).Length;
            ZSize = (UInt32)GetCompressedSize(File.ReadAllBytes(filePath));

            TimeStamp = (ulong)0x0000000000000000;
            
        }

        public static int GetCompressedSize(byte[] content)
        {
            return LZ4.LZ4Codec.EncodeHC(content, 0, content.Length).Length;
        }
    }
}