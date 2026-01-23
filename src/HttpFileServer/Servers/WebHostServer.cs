using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HttpFileServer.Handlers;
using HttpFileServer.Services;

namespace HttpFileServer.Servers
{
    /// <summary>
    /// 以html web服务器模式运行 响应标头不触发下载,优先从目录加载html页面
    /// </summary>
    public class StaticWebHostServer : DefaultFileServer
    {
        public StaticWebHostServer(int port, string path, bool enableJson, bool enableUpload = false) : base(port, path, enableJson, enableUpload)
        {
        }

        protected override void InitHandler()
        {
            // Use web-host specific handlers which return real mime types for files
            // and prefer serving index.html when a directory is requested.
            RegisterHandler("HEAD", new WebHostHeadHandler(_rootDir, _cacheSrv, _jsonCacheSrv, _jsonService));

            RegisterHandler("GET", new WebHostGetHandler(_rootDir, _cacheSrv, _jsonCacheSrv, _jsonService, EnableUpload, _enableJson));

            if (EnableUpload)
                RegisterHandler("POST", new HttpPostHandler(_rootDir));
        }
    }
}
