﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WolvenKit.Common
{
    public interface IWitcherArchiveType
    {
        string TypeName { get; }
        string FileName { get; set; }
        
    }
}
