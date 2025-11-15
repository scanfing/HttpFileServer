using HttpFileServer.Core;
using HttpFileServer.Services;
using HttpFileServer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace HttpFileServer.Handlers
{
    public class HttpGetHandler : HttpHandlerBase
    {
        #region Fields

        private CacheService _cacheSrv;

        private CacheService _jsonCacheSrv;

        private JsonService _jsonSrv;

        #endregion Fields

        public bool EnableJson { get; private set; }

        #region Constructors

        public HttpGetHandler(string rootDir, CacheService cacheService, CacheService jsonCacheService, JsonService jsonService, bool enableUpload = false, bool enableJson = true) : base(rootDir)
        {
            _cacheSrv = cacheService;
            _jsonCacheSrv = jsonCacheService;
            _jsonSrv = jsonService;
            EnableUpload = enableUpload;
            EnableJson = enableJson;
        }

        #endregion Constructors

        #region Methods

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var useJson = request.AcceptTypes.Any(p => p.Equals("application/json", StringComparison.OrdinalIgnoreCase));
            if (useJson && EnableJson)
            {
                await ProcessJsonRequest(context);
                return;
            }

            var zipDownload = false;
            var downloadQuery = request.QueryString.Get("download");
            if ("1".Equals(downloadQuery) || "true".Equals(downloadQuery, StringComparison.OrdinalIgnoreCase))
            {
                zipDownload = true;
            }
            else
            {
                zipDownload = request.AcceptTypes.Any(p => p.Equals("application/zip", StringComparison.OrdinalIgnoreCase));
            }

            if (zipDownload)
            {
                await ProcessZipRequest(context);
                return;
            }

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

        protected virtual async Task ProcessJsonRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');
            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _jsonCacheSrv.GetPathCacheId(dstpath);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                response.AppendHeader("Etag", cacheTag);
                //文件
                if (File.Exists(dstpath))
                {
                    if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                        await ResponseContentPartial(dstpath, request, response);
                    else
                        await ResponseContentFull(dstpath, request, response);
                    return;
                }

                //目录
                response.AddHeader("Content-Type", "application/json");

                var buff = _jsonCacheSrv.GetCache(dstpath);

                if (buff is null)
                {
                    DirInfoResponse respObj = new DirInfoResponse();
                    var dirinfo = new DirectoryInfo(dstpath);
                    respObj.LastWriteTime = dirinfo.LastWriteTime;
                    respObj.Name = dirinfo.Name;
                    respObj.RelativePath = Path.GetFullPath(dstpath).Replace(SourceDir, "/");

                    var dirs = Directory.GetDirectories(dstpath);
                    var files = Directory.GetFiles(dstpath);
                    var dlst = new List<DirPathInfo>();
                    var flst = new List<FilePathInfo>();
                    foreach (var p in dirs)
                    {
                        var dinfo = p.GetPathInfo(SourceDir) as DirPathInfo;
                        dlst.Add(dinfo);
                    }
                    foreach (var f in files)
                    {
                        var finfo = f.GetPathInfo(SourceDir) as FilePathInfo;
                        flst.Add(finfo);
                    }
                    respObj.Directories = dlst.ToArray();
                    respObj.Files = flst.ToArray();

                    var content = _jsonSrv.SerializeObject(respObj);
                    buff = Encoding.UTF8.GetBytes(content);
                    _jsonCacheSrv.SaveCache(dstpath, buff);
                }

                response.ContentLength64 = buff.LongLength;
                var stream = new MemoryStream(buff);
                await stream.CopyToAsync(response.OutputStream);
            }
        }

        protected async Task<bool> ProcessZipRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var resp = context.Response;

            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var path = tmp.Replace('/', '\\');

            resp.ContentType = "application/zip";
            resp.ContentEncoding = Encoding.UTF8;
            var dirname = Path.GetFileName(path);
            if (request.Url.LocalPath != "/")
            {
                dirname = Path.GetFileName(request.Url.LocalPath).Trim('\\').Trim();
            }

            dirname = dirname.Replace(SourceDir, "");
            var dsiposition = $"attachment; filename={Uri.EscapeUriString(dirname)}.zip";
            try
            {
                resp.Headers.Set("Content-Disposition", dsiposition);
            }
            catch (Exception ex)
            {
            }

            var memStream = new MemoryStream();
            using (var archive = new ZipArchive(memStream, ZipArchiveMode.Update, true))
            {
                if (Directory.Exists(path))
                {
                    var subdirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var subFileName = file.Replace(path, "").Trim('\\').Trim();
                        System.Diagnostics.Trace.WriteLine($"{DateTime.Now} ZIP -> {subFileName}");
                        // 添加文件到ZIP存档中
                        var zipEntry = archive.CreateEntry(subFileName);
                        using (var entryStream = zipEntry.Open())
                        {
                            using (var fileStream = File.OpenRead(file))
                            {
                                await fileStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }
                else if (File.Exists(path))
                {
                    var file = path;
                    var relfilename = Path.GetFileName(file);
                    var zipEntry = archive.CreateEntry(relfilename);
                    using (var entryStream = zipEntry.Open())
                    {
                        using (var fileStream = File.OpenRead(file))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
            System.Diagnostics.Trace.TraceInformation($"{DateTime.Now} {dirname}.zip Zip Done, Length: {memStream.Length} ");
            resp.ContentLength64 = memStream.Length;
            memStream.Position = 0;
            var buff = new byte[81920];
            while (true)
            {
                var count = memStream.Read(buff, 0, 81920);
                await resp.OutputStream.WriteAsync(buff, 0, count);
                await resp.OutputStream.FlushAsync();
                if (count < 81920)
                {
                    break;
                }
            }
            //await memStream.CopyToAsync(resp.OutputStream);
            memStream.Close();
            memStream.Dispose();
            memStream = null;
            await resp.OutputStream.FlushAsync();
            resp.StatusCode = 200;

            System.Diagnostics.Trace.TraceInformation($"{DateTime.Now} {dirname}.zip Response Done.");
            return true;
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
            var bytesNeeds = rangeEnd - range.Item1 + 1;
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            try
            {
                if (onlyHead)
                    return;

                stream.Seek(range.Item1, SeekOrigin.Begin);
                while (response.OutputStream.CanWrite)
                {
                    var readcount = stream.Read(buff, 0, 81920);
                    response.OutputStream.Write(buff, 0, (int)Math.Min(bytesNeeds, readcount));
                    if (readcount >= bytesNeeds)
                        break;

                    bytesNeeds -= readcount;
                    await Task.Delay(1);
                }
                if (response.OutputStream.CanWrite)
                    await response.OutputStream.FlushAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.Message);
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