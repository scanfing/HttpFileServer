using HttpFileServer.Core;
using HttpFileServer.Handlers;
using HttpFileServer.Models;
using HttpFileServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Servers
{
    public class JsonApiServer : HttpFileServerBase
    {
        #region Fields

        private CacheService _cacheSrv;

        private string _rootDir;

        #endregion Fields

        #region Constructors

        public JsonApiServer(int port, string path, bool enableUpload) : base(port, path, enableUpload)
        {
            EnableUpload = enableUpload;

            _rootDir = path;
            _cacheSrv = new CacheService();

            InitHandler();
        }

        #endregion Constructors

        #region Methods

        private void InitHandler()
        {
            RegisterHandler("GET", new HttpJsonGetHandler(_rootDir, _cacheSrv));
            if (EnableUpload)
                RegisterHandler("POST", new HttpPostHandler(_rootDir));
        }

        #endregion Methods
    }
}