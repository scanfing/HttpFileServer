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
    /// GET handler for Static Web Host behavior:
    /// - When requesting a directory, try to serve index.html in that directory first.
    /// - When requesting a file, use the real mime type for the response Content-Type.
    /// Otherwise falls back to default behaviors from HttpGetHandler.
    /// </summary>
    public class WebHostGetHandler : HttpGetHandler
    {
        public WebHostGetHandler(string rootDir, CacheService cacheService, CacheService jsonCacheService, JsonService jsonService, bool enableUpload = false, bool enableJson = true)
            : base(rootDir, cacheService, jsonCacheService, jsonService, enableUpload, enableJson)
        {
        }

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // If URL maps to a directory, prefer serving index.html inside it
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
                        // Serve first found index file with real mime type
                        await ResponseContentFull(indexPath, request, response, false, true);
                        return;
                    }
                }
            }

            // For file requests, use real mime type
            var potentialFile = dstpath;
            if (File.Exists(potentialFile))
            {
                await ResponseContentFull(potentialFile, request, response, false, true);
                return;
            }

            // Otherwise fallback to base behavior (directory listing, json, zip, etc.)
            await base.ProcessRequest(context);
        }
    }
}
