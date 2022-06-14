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
    public class HttpGetHandler : HttpHandlerBase
    {
        #region Fields

        private CacheService _cacheSrv;

        #endregion Fields

        #region Constructors

        public HttpGetHandler(string rootDir, CacheService cacheService, bool enableUpload = false) : base(rootDir)
        {
            _cacheSrv = cacheService;
            EnableUpload = enableUpload;
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
                    await ResponseContentPartial(dstpath, request, response);
                else
                    await ResponseContentFull(dstpath, request, response);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        /// <returns><see cref="Tuple$lt;string, Stream, bool&gt;"/> path,Stream,fileExist</returns>
        protected async Task<Tuple<string, Stream, bool>> GetResponseContentTypeAndStream(string path)
        {
            string contentType = "text/html";
            Stream stream = null;
            var fileExist = false;

            var data = _cacheSrv.GetCache(path);
            if (data is null)
            {
                if (File.Exists(path))
                {
                    await FileAccessHelper.AddAccessCount(path);
                    fileExist = true;
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    contentType = "application/octet-stream";
                }
                else if (Directory.Exists(path))
                {
                    var location = Path.GetFileName(path);
                    var title = "HttpFileServer";
                    if (path != SourceDir)
                    {
                        location = Path.GetFileName(SourceDir) + "\\" + path.Replace(SourceDir, "").Trim('\\');
                        title = Path.GetFileName(path.TrimEnd('\\')) + " -- HttpFileServer";
                    }
                    var content = HtmlExtension.GenerateHtmlContentForDir(path, path != SourceDir, EnableUpload, location, title);
                    data = Encoding.UTF8.GetBytes(content);
                    _cacheSrv.SaveCache(path, data);
                    stream = new MemoryStream(data);
                }
            }
            else
            {
                stream = new MemoryStream(data);
            }

            return new Tuple<string, Stream, bool>(contentType, stream, fileExist);
        }

        /// <summary>
        /// 响应全数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        protected async Task ResponseContentFull(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false)
        {
            var tp = await GetResponseContentTypeAndStream(path);
            response.AddHeader("Content-Type", tp.Item1);
            var stream = tp.Item2;
            if (stream is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            response.ContentLength64 = stream.Length;
            try
            {
                await stream.CopyToAsync(response.OutputStream);
                await response.OutputStream.FlushAsync();
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception)
            {
                //网络问题导致response.OutputStream无法写入
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                if (tp.Item3)
                {
                    FileAccessHelper.SubAccessCount(path);
                }
                stream.Close();
            }
        }

        /// <summary>
        /// 断点续传响应支持
        /// </summary>
        /// <returns></returns>
        protected async Task ResponseContentPartial(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false)
        {
            var rangeStr = request.Headers["Range"];
            var range = GetRequestRange(rangeStr);

            if (range.Item1 < 0)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }
            var tp = await GetResponseContentTypeAndStream(path);
            response.AddHeader("Content-Type", tp.Item1);
            var stream = tp.Item2;
            if (stream is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            if (range.Item1 > stream.Length || range.Item2 > stream.Length)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }

            response.AddHeader("Content-Range", $"bytes {range.Item1}-{range.Item2}/{stream.Length}");

            var buff = new byte[81920];
            var rangeEnd = range.Item2 > range.Item1 ? range.Item2 : stream.Length - 1;
            var bytesNeeds = rangeEnd - range.Item1;
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            try
            {
                if (onlyHead)
                    return;

                stream.Seek(range.Item1, SeekOrigin.Begin);
                while (bytesNeeds > 0)
                {
                    var readcount = stream.Read(buff, 0, 81920);
                    response.OutputStream.Write(buff, 0, (int)Math.Min(bytesNeeds, readcount));
                    bytesNeeds -= readcount;
                }

                await response.OutputStream.FlushAsync();
            }
            catch (Exception)
            {
                //网络问题导致response.OutputStream无法写入
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                if (tp.Item3)
                {
                    FileAccessHelper.SubAccessCount(path);
                }
                stream.Close();
            }
        }

        private Tuple<long, long> GetRequestRange(string rangeStr)
        {
            var rangeStart = 0L;
            var rangeEnd = 0L;
            var tmpstr = rangeStr.Replace("bytes=", "");
            var rgs = tmpstr.Split('-');

            if (!tmpstr.StartsWith("-"))
            {
                rangeStart = long.Parse(rgs[0]);
                if (!tmpstr.EndsWith("-"))
                {
                    rangeEnd = long.Parse(rgs[1]);
                }
            }
            else
            {
                if (!tmpstr.EndsWith("-"))
                {
                    rangeEnd = long.Parse(rgs[0]);
                }
            }

            return new Tuple<long, long>(rangeStart, rangeEnd);
        }

        #endregion Methods
    }
}