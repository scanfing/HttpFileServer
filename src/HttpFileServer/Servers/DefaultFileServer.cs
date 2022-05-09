using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpFileServer.Core;
using HttpFileServer.Handlers;
using HttpFileServer.Models;
using HttpFileServer.Services;
using HttpFileServer.Utils;

namespace HttpFileServer.Servers
{
    public class DefaultFileServer : HttpFileServerBase
    {
        #region Fields

        private CacheService _cacheSrv;

        private string _rootDir;

        #endregion Fields

        #region Constructors

        public DefaultFileServer(int port, string path, bool enableUpload = false) : base(port, path, enableUpload)
        {
            EnableUpload = enableUpload;

            _rootDir = path;
            _cacheSrv = CacheService.GetDefault();
        }

        #endregion Constructors

        #region Methods

        protected override void InitHandler()
        {
            GetHandler = new HttpGetHandler(_rootDir, _cacheSrv);
            HeadHandler = new HttpHeadHandler(_rootDir, _cacheSrv);
            if (EnableUpload)
                PostHandler = new HttpPostHandler(_rootDir);
        }

        #endregion Methods
    }
}