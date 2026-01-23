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

        /// <summary>
        /// 启动程序后自动开启服务（手动）
        /// </summary>
        public bool AutoStartOnLaunch { get; set; } = false;

        /// <summary>
        /// 开机自启动并启动服务
        /// </summary>
        public bool AutoStartWithSystem { get; set; } = false;

        public bool EnableUpload { get; set; } = false;

        /// <summary>
        /// 开机自启动后最小化到托盘
        /// </summary>
        public bool MinimizeToTrayAfterAutoStart { get; set; } = true;

        /// <summary>
        /// HTTP 服务端口
        /// </summary>
        public ushort Port { get; set; } = 80;

        public List<string> RecentDirs { get; set; }

        public string RootDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        /// <summary>
        /// 主题模式 (Light / Dark / System)
        /// </summary>
        public string ThemeMode { get; set; } = "System";

        /// <summary>
        /// 是否以静态 Web 服务器模式运行（优先返回 index.html、使用真实 MIME）
        /// </summary>
        public bool UseWebServer { get; set; } = false;

        #endregion Properties
    }
}