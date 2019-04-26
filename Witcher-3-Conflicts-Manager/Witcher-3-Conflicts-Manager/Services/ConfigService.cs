using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Witcher_3_Conflicts_Manager.Services
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class ConfigService : ObservableObject, IConfigService
    {
        private string _TW3_Path;
        private string _WCC_Path;

        private const string _TW3_key = "TW3_Path";
        private const string _WCC_key = "WCC_Path";

        public ConfigService()
        {
            Load();
        }

        public string GetConfigSetting(string configKey)
        {
            if (configKey == _TW3_key)
            {
                return _TW3_Path;
            }
            if (configKey == _WCC_key)
            {
                return _WCC_Path;
            }
            throw new NotImplementedException();
        }

        public void SetConfigSetting(string configKey, string value)
        {
            if (configKey == _TW3_key)
            {
                _TW3_Path = value;
                return;
            }
            if (configKey == _WCC_key)
            {
                _WCC_Path = value;
                return;
            }
            throw new NotImplementedException();
        }



        /// <summary>
        /// Attempts to load the Config from the xml
        /// returns false if the file does not exists or could not read variables or variables actually point to files
        /// </summary>
        /// <returns></returns>
        public bool Load()
        {
            string outfile = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
            if (File.Exists(outfile))
            {
                try
                {
                    XDocument doc = XDocument.Load(outfile);

                    _TW3_Path = doc.Descendants(_TW3_key)?.First().Value.ToString();
                    _WCC_Path = doc.Descendants(_WCC_key)?.First().Value.ToString();

                    //if any are empty return false
                    return File.Exists(_TW3_Path) && File.Exists(_WCC_Path);

                }
                catch (Exception ex)
                {
                    throw ex;
                    // file is bugged and should be deleted
                    File.Delete(outfile); //FIXME
                    return false;
                }
            }
            else // no settings file
            {
                return false;
            }
        }

        /// <summary>
        /// Saves Settings to xml.
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            // write to xml
            try
            {
                XDocument doc = new XDocument(
                    new XElement("Settings",
                        new XElement(_TW3_key, _TW3_Path),
                        new XElement(_WCC_key, _WCC_Path)
                    )
                );
                string outfile = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
                doc.Save(outfile);

                return File.Exists(outfile);
            }
            catch (Exception e)
            {
                throw e;
                return false;
            }
           
        }

    


    }
    
}
