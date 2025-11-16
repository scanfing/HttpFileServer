using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    public class Config
    {
        #region Properties

        public bool EnableUpload { get; set; } = false;
        public ushort Port { get; set; } = 80;
        public List<string> RecentDirs { get; set; }
        public string RootDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        #endregion Properties
    }
}