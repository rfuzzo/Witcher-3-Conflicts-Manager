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
    public class LoggerService : ObservableObject, ILoggerService
    {
        public LoggerService()
        {
        }

        #region Properties
        public string Log { get; set; } = "Log initialized.\n\r";
        #endregion

        #region Overrides
        public override string ToString() => Log;
        #endregion

        #region Methods
        /// <summary>
        /// Log an string
        /// </summary>
        /// <param name="value"></param>
        public void LogString(string value)
        {

            Log += value + "\r\n";
            OnPropertyChanged("Log");
        }
       

        public void Clear()
        {
            Log = "";
        }
        #endregion



    }
}
