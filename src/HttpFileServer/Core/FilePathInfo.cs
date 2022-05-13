using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    [DataContract]
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

        [DataMember]
        public string FileSize { get; set; }

        [DataMember]
        public long Length { get; set; }

        #endregion Properties
    }
}