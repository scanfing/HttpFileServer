using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Xml.Linq;

namespace HttpFileServer.Utils
{
    public class AutoStartHelper
    {
        private const string AutoRunRegPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

        public static bool IsProcessRunWithAutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(a => a.Equals("-autorun", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 取消自启动
        /// </summary>
        /// <returns></returns>
        public static bool SetAutoStart(bool autoStart = true)
        {
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            exePath = "\"" + exePath + "\" -autorun";

            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.CreateSubKey(AutoRunRegPath);
                regKey?.DeleteValue("HttpFileServer", false);

                if (autoStart)
                {
                    regKey?.SetValue("HttpFileServer", exePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("设置自启动失败：" + ex.Message);
                return false;
            }
            finally
            {
                regKey?.Close();
            }
        }
    }
}