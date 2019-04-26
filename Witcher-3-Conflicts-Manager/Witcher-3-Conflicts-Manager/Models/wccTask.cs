using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Witcher_3_Conflicts_Manager.Models
{
    using Services;
    using System.Diagnostics;
    using System.IO;

    public class WCC_Task
    {
        /*private IConfigService Config { get; set; }
        private ILoggerService Logger { get; set; }

        [Inject]
        public void SetConfigService(IConfigService configService, ILoggerService loggerService)
        {
            Config = configService;
            Logger = loggerService;
        }*/

        /// <summary>
        /// runs wcc_lite with specified command
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        /*public WFR RunCommandSync(WCC_Command cmd)
        {
            string args = cmd.CommandLine;
            return RunArgsSync(cmd.Name, args);
        }*/

        /// <summary>
        /// Runs wcc_lite with specified arguments
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool RunArgsSync(string cmdName, string args)
        {
            //string wccPath = Config.GetConfigSetting("WCC_Path");
            string wccPath = Properties.Settings.Default.WCC_Path;
            var proc = new ProcessStartInfo(wccPath) { WorkingDirectory = Path.GetDirectoryName(wccPath) };

            try
            {
                //Logger.LogString($"-----------------------------------------------------");
                //Logger.LogString($"WCC_TASK: {args}");

                proc.Arguments = args;
                proc.UseShellExecute = false;
                proc.RedirectStandardOutput = true;
                proc.WindowStyle = ProcessWindowStyle.Hidden;
                proc.CreateNoWindow = true;

                using (var process = Process.Start(proc))
                {
                    using (var reader = process.StandardOutput)
                    {
                        while (true)
                        {
                            string result = reader.ReadLine();

                            //Logger.LogString(result);
                            //Logger.LogExtended(SystemLogFlag.SLF_Interpretable, ToolFlag.TLF_Wcc, cmdName, $"{result}");

                            if (reader.EndOfStream)
                                break;
                        }
                    }
                }

                //Handle Errors
                /*if (Logger.ExtendedLog.Any(x => x.Flag == LogFlag.WLF_Error))
                {
                    Logger.LogString("Finished with Errors.");
                    return WFR.WFR_Error;
                }
                else if (Logger.ExtendedLog.Any(x => x.Flag == LogFlag.WLF_Error))
                {
                    Logger.LogString("Finished with Warnings.");
                    return WFR.WFR_Finished;
                }
                else
                {
                    Logger.LogString("Finished without Errors or Warnings.");
                    return WFR.WFR_Finished;
                }*/
                return true;
            }
            catch (Exception ex)
            {
                //Logger.LogString(ex.ToString());
                throw ex;

            }
        }
    }
}
