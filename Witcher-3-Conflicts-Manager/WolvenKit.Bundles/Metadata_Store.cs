using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;
using WolvenKit.Common;

namespace WolvenKit.Bundles
{
    using Types;

    public class Metadata_Store
    {
        #region Info
        public static byte[] IDString = { 0x03, 0x56, 0x54, 0x4D }; // ".VTM"
        public static Int32 Version = 6;
        public static UInt32 MaxFileSizeInBundle;
        public static UInt32 MaxFileSIzeInMemory;
        #endregion

        #region Fields
        List<byte> FileStringTable = new List<byte>();
        TDynArray<UFileInfo> fileInfoList = new TDynArray<UFileInfo>();
        TDynArray<UFileEntryInfo> fileEntryInfoList = new TDynArray<UFileEntryInfo>();
        TDynArray<UBundleInfo> bundleInfoList = new TDynArray<UBundleInfo>();
        List<Int32> Buffers = new List<int>();
        TDynArray<UDirInitInfo> dirInitInfoList = new TDynArray<UDirInitInfo>();
        TDynArray<UFileInitInfo> fileInitInfoList = new TDynArray<UFileInitInfo>();
        TDynArray<UHash> hashes = new TDynArray<UHash>();
        #endregion

        public Metadata_Store()
        {

        }

        public Metadata_Store(params Bundle[] Bundles)
        {
            Read(Bundles);
        }

        public Metadata_Store(string inDir)
        {
            BundleManager bm = new BundleManager();
            List<string> dirs = new List<string>();
            dirs.Add(inDir);
            dirs.AddRange(Directory.GetDirectories(inDir));
            dirs.Sort(new AlphanumComparator<string>());

            var modbundles = dirs.SelectMany(dir => Directory.GetFiles(dir, "*.bundle", SearchOption.AllDirectories)).ToList();
            foreach (var file in modbundles)
                bm.LoadModBundle(file);

            List<Bundle> bundles = bm.Bundles.ToList().Select(_ => _.Value).ToList();
            Read(bundles.ToArray());
        }

        /// <summary>
        /// Reads a Metadata_Store from a metadata.store file.
        /// </summary>
        /// <param name="filepath"></param>
        public void Read(string filepath)
        {
            Console.WriteLine("Reading: " + filepath);
            using (var br = new BinaryReader(new FileStream(filepath, FileMode.Open)))
            {
                if (!br.ReadBytes(4).SequenceEqual(IDString))
                    throw new InvalidDataException("Wrong Magic when reading the metadata.store file!");
                Version = br.ReadInt32();
                MaxFileSizeInBundle = br.ReadUInt32();
                MaxFileSIzeInMemory = br.ReadUInt32();
                var StringTableSize = br.ReadVLQInt32();
                //Read the string table
                /*
                 empty line => ""
                <everything>
                empty line => ""
                parts so stuff like 
                bundles\\buffers.bundle is here split 
                by the \\ for the non bundles this is 
                basically if you would 
                do a virtual tree inside the bundles
                one more empty line at the end=> ""
                 */
                FileStringTable = br.ReadBytes(StringTableSize).ToList();

                //Read the file infos
                fileInfoList = new TDynArray<UFileInfo>();
                fileInfoList.Deserialize(br);
                

                using (var ms = new MemoryStream(FileStringTable.ToArray()))
                {
                    using (var brr = new BinaryReader(ms))
                    {
                        foreach (var inf in fileInfoList)
                        {
                            brr.BaseStream.Seek(inf.StringTableNameOffset, SeekOrigin.Begin);
                            inf.path = ReadCR2WString(brr);
                        }
                    }
                }

                //Read the file entry infos
                fileEntryInfoList = new TDynArray<UFileEntryInfo>();
                fileEntryInfoList.Deserialize(br);
                
                //Read the Bundle Infos
                bundleInfoList = new TDynArray<UBundleInfo>();
                bundleInfoList.Deserialize(br);

                //Read the buffers
                var buffercount = br.ReadVLQInt32();
                if (buffercount > 0)
                {
                    for (int i = 0;i < buffercount;i++)
                    {
                        Buffers.Add(br.ReadInt32());
                    }
                }
         
                //Read dir initialization infos
                dirInitInfoList = new TDynArray<UDirInitInfo>();
                dirInitInfoList.Deserialize(br);

                //File initialization infos
                fileInitInfoList = new TDynArray<UFileInitInfo>();
                fileInitInfoList.Deserialize(br);

                //Hashes
                hashes = new TDynArray<UHash>();
                hashes.Deserialize(br);

                if(br.BaseStream.Position == br.BaseStream.Length)
                    Console.WriteLine("Succesfully read everything!");
                else
                {
                    Console.WriteLine($"Reader is at {br.BaseStream.Position} bytes. The length of the file is { br.BaseStream.Length} bytes.\n{ br.BaseStream.Length-br.BaseStream.Position} bytes wasn't read.");
                }
            }
        }

        /// <summary>
        /// Reads a Metadata_Store from a list of bundles.
        /// </summary>
        /// <param name="Bundles"></param>
        public void Read(params Bundle[] Bundles)
        {
            #region Info
            FileStringTable.Add( 0x00 );
            fileInfoList.Add( new UFileInfo() );
            fileEntryInfoList.Add( new UFileEntryInfo() );
            bundleInfoList.Add( new UBundleInfo() );
            #endregion
            

            var stOffsetDict = new Dictionary<string, uint>();
            var _entries = new List<IWitcherFile>();

            var _entryNames = new List<string>();
            var _bufferNames = new List<string>();
            var _bundleNames = new List<string>();
            var _fileNames = new List<string>();
            var _dirNames = new List<string>();

            var _dirInfos = new List<DirectoryInfo>();
            var _fileInfos = new List<FileInfo>();

            #region Dir and Files Table
            foreach (var b in Bundles)
            {
                string bundleName = b.Name;
                _bundleNames.Add(bundleName);

                stOffsetDict.Add(bundleName, (uint)FileStringTable.Count);
                FileStringTable.AddRange(Encoding.UTF8.GetBytes(bundleName));
                FileStringTable.Add(0x00);
                
                foreach (var item in b.ItemsList)
                {
                    string relFullPath = item.Name;
                    if (_entryNames.Contains(relFullPath))
                        continue;

                    // stringtable: files
                    stOffsetDict.Add(relFullPath, (uint)FileStringTable.Count);
                    FileStringTable.AddRange(Encoding.UTF8.GetBytes(relFullPath));
                    FileStringTable.Add(0x00);

                    //add buffername
                    if (relFullPath.Split('.').Last() == "buffer")
                    {
                        //add to buffer list
                        string buffername = relFullPath.Split(new string[] { ".1.buffer" }, StringSplitOptions.None).First();
                        if (_bufferNames.Contains(relFullPath))
                            continue;
                        _bufferNames.Add(buffername);
                    }
                    else
                    {
                        // stringtable: dir and file names
                        FileInfo fi = new FileInfo($"\\{relFullPath}");
                        _fileInfos.Add(fi);
                        if (!_fileNames.Contains(fi.Name))
                            _fileNames.Add(fi.Name);
                        var dirs = relFullPath.Split('\\').ToList();
                        dirs.Remove(dirs.Last());
                        _dirNames.AddRange(dirs.Where(_ => !_dirNames.Contains(_)));
                    }

                    // add to entry list
                    _entryNames.Add(relFullPath);
                    _entries.Add(item);
                }
            }

            FileStringTable.Add(0x00);
            foreach (var d in _dirNames)
            {
                stOffsetDict.Add(d, (uint)FileStringTable.Count);
                FileStringTable.AddRange(Encoding.UTF8.GetBytes(d));
                FileStringTable.Add(0x00);
            }
            foreach (var f in _fileNames)
            {
                stOffsetDict.Add(f, (uint)FileStringTable.Count);
                FileStringTable.AddRange(Encoding.UTF8.GetBytes(f));
                FileStringTable.Add(0x00);
            }

            int StringTableSize = FileStringTable.Count;
            #endregion

            #region UFileInitInfo and Hashes
            foreach (var fi in _fileInfos)
            {
                //add directoryInfo to Directory info list
                DirectoryInfo di = fi.Directory;
                while (true)
                {
                    if (_dirInfos.Select(_ => _.Name).Contains(di.Name))
                        break;

                    _dirInfos.Add(di);
                    di = di.Parent;
                    if (di.Parent == null)
                        break;
                }

                var fii = new UFileInitInfo()
                {
                    FileIF = _fileInfos.IndexOf(fi) + 1,
                    DirID = (Int32)_dirNames.IndexOf(fi.Directory.Name) + 1,
                    Name = (Int32)stOffsetDict[fi.Name]
                };
                fileInitInfoList.Add(fii);

                var fullname = _entries.First(_ => _.Name.Split('\\').Last() == fi.Name).Name;
                UInt64 hash = (UInt64)FNV1a.HashFNV1a64(fullname);
                var h = new UHash()
                {
                    Hash = (UInt64)hash,
                    FileID = (UInt64)_fileInfos.IndexOf(fi) + 1
                };
                hashes.Add(h);
            }
            //hashes are sorted by hashsize not by filenumber
            hashes.Sort((x, y) => x.Hash.CompareTo(y.Hash));
            #endregion

            #region UDirInitInfo
            //first directoryinfo i null with offset to the first 0 byte of the dir Table
            _dirInfos.Reverse();
            dirInitInfoList.Add(new UDirInitInfo()
            {
                ParentID = 0,
                Name = (Int32)stOffsetDict[_dirNames.First()] - 1
            });
            foreach (var di in _dirInfos)
            {
                Int32 parentID = 0;
                if (di.Parent.Parent != null)
                    parentID = (int)_dirNames.IndexOf(di.Parent.Name) + 1;

                var dii = new UDirInitInfo()
                {
                    ParentID = parentID,
                    Name = (Int32)stOffsetDict[di.Name]
                };
                dirInitInfoList.Add(dii);
            }
            #endregion

            #region UBundleInfo
            foreach (var b in Bundles)
            {
                string bundleName = b.Name;

                IWitcherFile ffe = _entries.First(_ => _.Bundle.Name == bundleName);

                var bi = new UBundleInfo()
                {
                    Name = stOffsetDict[bundleName],
                    FirstFileEntry = (UInt32)(_entries.IndexOf(ffe) + 1),
                    NumBundleEntries = (UInt32)b.ItemsList.Count,
                    DataBlockSize = b.DataBlockSize, //this is wrong for Buffers //FIXME
                    DataBlockOffset = b.DataBlockOffset,
                    BurstDataBlockSize = 0,
                };
                bundleInfoList.Add(bi);
            }
            #endregion

            #region UFileInfos and UFileEntryInfos
            for (int i = 0; i < _entries.Count; i++)
            {
                IWitcherFile e = _entries[i];


                UInt32 bufferid = 0;
                UInt32 hasbuffer = 0;
                if (_bufferNames.Contains(e.Name))
                {
                    hasbuffer = 1;
                    bufferid = (uint)_bufferNames.IndexOf(e.Name);
                }

                var fi = new UFileInfo()
                {
                    StringTableNameOffset = stOffsetDict[e.Name],
                    PathHash = 0, //FIXME this is always 0...
                    SizeInBundle = e.ZSize,
                    SizeInMemory = (UInt32)e.Size,
                    FirstEntry = (UInt32)(_entries.IndexOf(e) + 1),
                    CompressionType = e.Compression,
                    bufferid = bufferid,
                    hasbuffer = hasbuffer
                };
                fileInfoList.Add(fi);

                string bundleName = e.Bundle.Name;
                var fei = new UFileEntryInfo()
                {
                    FileID = (uint)i + 1,
                    BundleID = (uint)_bundleNames.ToList().IndexOf(bundleName) + 1,
                    OffsetInBundle = (uint)e.PageOFfset,
                    SizeInBundle = e.ZSize,
                    NextEntry = 0
                };
                fileEntryInfoList.Add(fei);
            }
            #endregion

            #region Buffers
            foreach (var buffer in _entries.Where(_ => _.Name.Split('.')?.Last() == "buffer"))
                Buffers.Add(_entries.IndexOf(buffer) + 1);
            #endregion

            MaxFileSizeInBundle = fileInfoList.Select(_ => _.SizeInBundle).ToList().Max();
            MaxFileSIzeInMemory = fileInfoList.Select(_ => _.SizeInMemory).ToList().Max();
        }

        /// <summary>
        /// Serialize to a file from a list of bundles.
        /// </summary>
        /// <param name="outDir"></param>
        /// <param name="Bundles"></param>
        public void Write(string outDir)
        {
            var filePath = Path.Combine(outDir, "metadata.store");
            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                // header
                bw.Write(IDString);
                bw.Write((Int32)Version);
                bw.Write((Int32)MaxFileSizeInBundle);
                bw.Write((Int32)MaxFileSIzeInMemory);
                // string table (file names, individual strings)
                bw.WriteVLQInt32(FileStringTable.Count);
                bw.Write(FileStringTable.ToArray());
                // write UFileInfos
                fileInfoList.Serialize(bw);
                // write UFileEntryInfos
                fileEntryInfoList.Serialize(bw);
                // write UBundleInfos
                bundleInfoList.Serialize(bw);
                // write Buffers
                bw.WriteVLQInt32(Buffers.Count);
                foreach (var index in Buffers)
                    bw.Write((UInt32)index);
                // write UDirInitInfos
                dirInitInfoList.Serialize(bw);
                // write UFileInitInfos
                fileInitInfoList.Serialize(bw);
                // write UHashes
                hashes.Serialize(bw);

            }
        }



        /// <summary>
        /// Serialize to a file from a directory.
        /// </summary>
        /// <param name="outDir"></param>
        /// <param name="inDir"></param>
        public static void Write(string outDir, string inDir)
        {
            Metadata_Store store = new Metadata_Store(inDir);
            store.Write(outDir);
        }



        public void cwdump(object obj, BinaryReader br)
        {
            Console.WriteLine("Dumping object: " + obj.GetType().Name);
            Console.WriteLine(ObjectDumper.Dump(obj));
            Console.WriteLine("Br is at: " + br.BaseStream.Position + "[0x" + br.BaseStream.Position.ToString("X") + "] left: " + ((int)br.BaseStream.Length - br.BaseStream.Position) + "[0x" + ((int)br.BaseStream.Length - br.BaseStream.Position).ToString("X") + "]");
            Console.WriteLine();

        }
        public static string ReadCR2WString(BinaryReader br, int len = 0)
        {
            if (br.BaseStream.Position >= br.BaseStream.Length)
                throw new IndexOutOfRangeException();
            string str = null;
            if (len > 0)
            {
                str = Encoding.Default.GetString(br.ReadBytes(len));
            }
            else
            {
                bool shouldread = true;
                while (shouldread)
                {
                    if (br.BaseStream.Position >= br.BaseStream.Length) //mallformed string not closed by '\0' properly
                        throw new IndexOutOfRangeException();
                    var c = br.ReadByte();
                    str += (char)c;
                    shouldread = (c != 0);
                }
            }
            return str;
        }
    }

    
    

    

    

    

    

    
}
