using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Witcher_3_Conflicts_Manager.Services
{

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public interface IConfigService
    {
        string GetConfigSetting(string configKey);

        void SetConfigSetting(string configKey, string value);

        bool Save();

        bool Load();
    }
}
