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

        private bool _enableJson;
        private string _rootDir;

        #endregion Fields

        #region Constructors

        public DefaultFileServer(int port, string path, bool enableJson, bool enableUpload = false) : base(port, path, enableUpload)
        {
            _rootDir = path;
            _cacheSrv = CacheService.GetDefault();
            _enableJson = enableJson;

            InitHandler();
        }

        #endregion Constructors

        #region Methods

        protected void InitHandler()
        {
            RegisterHandler("HEAD", new HttpHeadHandler(_rootDir, _cacheSrv));

            RegisterHandler("GET", new HttpGetHandler(_rootDir, _cacheSrv));
            if (_enableJson)
                RegisterHandler("GET", new HttpJsonGetHandler(_rootDir, new CacheService()));

            if (EnableUpload)
                RegisterHandler("POST", new HttpPostHandler(_rootDir));
        }

        protected override void OnLocalFileSrv_DirContentChanged(object sender, string path)
        {
            _cacheSrv.Delete(path);
            base.OnLocalFileSrv_DirContentChanged(sender, path);
        }

        protected override void OnLocalFileSrv_PathDeleted(object sender, string path)
        {
            _cacheSrv.Delete(path);
            base.OnLocalFileSrv_PathDeleted(sender, path);
        }

        #endregion Methods
    }
}