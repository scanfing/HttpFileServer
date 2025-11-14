using HttpFileServer.Services;
using HttpFileServer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Handlers
{
    public class HttpHeadHandler : HttpGetHandler
    {
        #region Fields

        private CacheService _cacheSrv;

        #endregion Fields

        #region Constructors

        public HttpHeadHandler(string rootDir, CacheService cacheService, CacheService jsonCacheService, JsonService jsonSrv) : base(rootDir, cacheService, jsonCacheService, jsonSrv)
        {
            _cacheSrv = cacheService;
        }

        #endregion Constructors

        #region Methods

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');
            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _cacheSrv.GetPathCacheId(dstpath);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                response.AppendHeader("Etag", cacheTag);
                if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                    await ResponseContentPartial(dstpath, request, response, true);
                else
                    await ResponseContentFull(dstpath, request, response, true);
            }
        }

        #endregion Methods
    }
}