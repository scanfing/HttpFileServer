using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    /// <summary>
    /// 目录信息响应对象
    /// </summary>
    public class DirInfoResponse : DirPathInfo
    {
        #region Properties

        public DirPathInfo[] Directories { get; set; }

        public FilePathInfo[] Files { get; set; }

        #endregion Properties
    }
}