using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Witcher_3_Conflicts_Manager.Services
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public interface ILoggerService
    {
       

        string Log { get; }

        void Clear();
        void LogString(string value);

    }
}
