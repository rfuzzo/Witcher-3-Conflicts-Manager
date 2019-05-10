using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Common
{
    public enum CompressionType
    {
        None = 0,
        ZLib = 1,
        Snappy = 2,
        Doboz = 3,
        LZ4 = 4,
        LZ4HC = 5,
    }
}
