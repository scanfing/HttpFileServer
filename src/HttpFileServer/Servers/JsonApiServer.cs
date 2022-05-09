using HttpFileServer.Core;
using HttpFileServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Servers
{
    public class JsonApiServer : HttpFileServerBase
    {
        #region Constructors

        public JsonApiServer(int port, string path, bool enableUpload) : base(port, path, enableUpload)
        {
        }

        #endregion Constructors

        #region Methods

        protected override void InitHandler()
        {
        }

        #endregion Methods
    }
}