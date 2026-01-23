using HttpFileServer.Services;
using HttpFileServer.Utils;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Handlers
{
    /// <summary>
    /// HEAD handler for Static Web Host behavior: mirror WebHostGetHandler but only head
    /// </summary>
    public class WebHostHeadHandler : HttpHeadHandler
    {
        public WebHostHeadHandler(string rootDir, CacheService cacheService, CacheService jsonCacheService, JsonService jsonSrv)
            : base(rootDir, cacheService, jsonCacheService, jsonSrv)
        {
        }

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');

            if (Directory.Exists(dstpath))
            {
                var defaultFiles = new[] { "index.html", "index.htm", "default.html", "default.htm" };
                foreach (var df in defaultFiles)
                {
                    var indexPath = Path.Combine(dstpath, df);
                    if (File.Exists(indexPath))
                    {
                        await ResponseContentFull(indexPath, request, response, true, true);
                        return;
                    }
                }
            }

            if (File.Exists(dstpath))
            {
                await ResponseContentFull(dstpath, request, response, true, true);
                return;
            }

            await base.ProcessRequest(context);
        }
    }
}
