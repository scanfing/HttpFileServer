using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    public class FilePathInfo : PathInfo
    {
        #region Constructors

        public FilePathInfo()
        {
            IsDirectory = false;
            IsFile = true;
        }

        #endregion Constructors

        #region Properties

        public string FileSize { get; set; }

        public long Length { get; set; }

        #endregion Properties
    }
}