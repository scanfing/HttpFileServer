using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    public class DirPathInfo : PathInfo
    {
        #region Constructors

        public DirPathInfo()
        {
            IsDirectory = true;
            IsFile = false;
        }

        #endregion Constructors
    }
}