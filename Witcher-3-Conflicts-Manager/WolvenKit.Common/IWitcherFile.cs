using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Common
{
    public interface IWitcherFile
    {
        IWitcherArchiveType Bundle { get; set; }
        string DepotPath { get; set; }
        long Size { get; set; }
        uint ZSize { get; set; }
        long PageOffset { get; set; }
        CompressionType CompressionType { get; }

        void Extract(Stream output);
        void Extract(string filename);
    }
}
