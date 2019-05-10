using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Bundles.Types
{
    /// <summary>
    /// 
    /// Accessor data for the original physical files to be packed or parsed
    /// Contains information about the filepath
    /// and an offset for reading as MemorymappedFile
    /// There are two types
    /// 1. BundleAccesors for reading data inside bundles (via pageoffsets)
    /// 2. FileAccessors for reading whole files (pageoffset is set to 0)
    /// 
    /// </summary>
    public interface IWitcherFileAccessor
    {
        string Path { get; set; }
        long Offset { get; set; }
    }

    public class FileAccessor : IWitcherFileAccessor
    {
        public FileAccessor(string filepath)
        {
            Path = filepath;
            Offset = 0;
        }

        public string Path { get; set; }
        public long Offset { get; set; }
    }

    public class BundleAccesor : IWitcherFileAccessor
    {
        public BundleAccesor(string bundlepath, long pageoffset)
        {
            Path = bundlepath;
            Offset = pageoffset;
        }

        public string Path { get; set; }
        public long Offset { get; set; }
    }
}
