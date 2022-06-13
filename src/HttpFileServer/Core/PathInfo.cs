using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    public class PathInfo
    {
        #region Constructors

        public PathInfo()
        {
            LastWriteTime = DateTime.Parse("1970-01-01");
        }

        #endregion Constructors

        #region Properties

        public bool IsDirectory { get; protected set; }

        public bool IsFile { get; protected set; }

        public DateTime LastWriteTime { get; set; }

        public string Name { get; set; }

        public string RelativePath { get; set; }

        #endregion Properties
    }
}