using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Core
{
    [DataContract]
    public class PathInfo
    {
        #region Properties

        [DataMember]
        public bool IsDirectory { get; protected set; }

        [DataMember]
        public bool IsFile { get; protected set; }

        [DataMember]
        public DateTime LastWriteTime { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string RelativePath { get; set; }

        #endregion Properties
    }
}