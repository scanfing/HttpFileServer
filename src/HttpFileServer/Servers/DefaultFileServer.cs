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

        protected CacheService _cacheSrv;

        protected bool _enableJson;
        protected CacheService _jsonCacheSrv;
        protected JsonService _jsonService;
        protected string _rootDir;

        protected bool _runAsWebHostServer;

        #endregion Fields

        #region Constructors

        public DefaultFileServer(int port, string path, bool enableJson, bool enableUpload = false) : base(port, path, enableUpload)
        {
            _rootDir = path;
            _cacheSrv = CacheService.GetDefault();
            _jsonCacheSrv = new CacheService();
            _enableJson = enableJson;
            _jsonService = new JsonService();

            InitHandler();
        }

        #endregion Constructors

        #region Methods

        protected virtual void InitHandler()
        {
            RegisterHandler("HEAD", new HttpHeadHandler(_rootDir, _cacheSrv, _jsonCacheSrv, _jsonService));

            RegisterHandler("GET", new HttpGetHandler(_rootDir, _cacheSrv, _jsonCacheSrv, _jsonService, EnableUpload, _enableJson));

            if (EnableUpload)
                RegisterHandler("POST", new HttpPostHandler(_rootDir));
        }

        protected override void OnLocalFileSrv_DirContentChanged(object sender, string path)
        {
            _cacheSrv.Delete(path);
            _jsonCacheSrv.Delete(path);
            base.OnLocalFileSrv_DirContentChanged(sender, path);
        }

        protected override void OnLocalFileSrv_PathDeleted(object sender, string path)
        {
            _cacheSrv.Delete(path);
            _jsonCacheSrv.Delete(path);
            base.OnLocalFileSrv_PathDeleted(sender, path);
        }

        #endregion Methods
    }
}